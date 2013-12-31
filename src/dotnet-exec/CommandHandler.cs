﻿// Copyright (c) Weihan Li. All rights reserved.
// Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using WeihanLi.Common.Models;

namespace Exec;

public sealed class CommandHandler : ICommandHandler
{
    private readonly ILogger _logger;
    private readonly ICompilerFactory _compilerFactory;
    private readonly IExecutorFactory _executorFactory;
    private readonly IUriTransformer _uriTransformer;
    private readonly IScriptContentFetcher _scriptContentFetcher;

    public CommandHandler(ILogger logger,
        ICompilerFactory compilerFactory,
        IExecutorFactory executorFactory,
        IUriTransformer uriTransformer,
        IScriptContentFetcher scriptContentFetcher)
    {
        _logger = logger;
        _compilerFactory = compilerFactory;
        _executorFactory = executorFactory;
        _uriTransformer = uriTransformer;
        _scriptContentFetcher = scriptContentFetcher;
    }

    public int Invoke(InvocationContext context) => InvokeAsync(context).GetAwaiter().GetResult();

    public async Task<int> InvokeAsync(InvocationContext context)
    {
        var parseResult = context.ParseResult;

        // 1. options binding
        var options = new ExecOptions();
        options.BindCommandLineArguments(parseResult);
        options.CancellationToken = context.GetCancellationToken();
        if (options.DebugEnabled)
        {
            _logger.LogDebug("options: {options}", JsonSerializer.Serialize(options, new JsonSerializerOptions()
            {
                Converters =
                {
                    new JsonStringEnumConverter()
                },
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            }));
        }

        return await Execute(options);
    }

    public async Task<int> Execute(ExecOptions options)
    {
        if (options.Script.IsNullOrWhiteSpace())
        {
            _logger.LogError("The file {ScriptFile} can not be empty", options.Script);
            return -1;
        }

        // exact reference and usings from project file
        if (options.ProjectPath.IsNotNullOrEmpty())
        {
            var startTime = Stopwatch.GetTimestamp();
            // https://learn.microsoft.com/en-us/dotnet/standard/linq/linq-xml-overview
            var projectPath = _uriTransformer.Transform(options.ProjectPath);
            var element = XElement.Load(projectPath);
            var itemGroups = element.Descendants("ItemGroup").ToArray();
            if (itemGroups.HasValue())
            {
                var usingElements = itemGroups.SelectMany(x => x.Descendants("Using"));
                foreach (var usingElement in usingElements)
                {
                    var usingText = usingElement.Attribute("Include")?.Value;
                    if (usingText.IsNotNullOrEmpty())
                    {
                        if (usingElement.Attribute("Static")?.Value == "true")
                        {
                            usingText = $"static {usingText}";
                        }

                        var alias = usingElement.Attribute("Alias")?.Value;
                        if (alias.IsNotNullOrEmpty())
                        {
                            usingText = $"{alias} = {usingText}";
                        }
                    }
                    else
                    {
                        usingText = usingElement.Attribute("Remove")?.Value;
                        if (usingText.IsNotNullOrEmpty())
                        {
                            usingText = $"- {usingText}";
                        }
                    }

                    if (usingText.IsNotNullOrEmpty())
                    {
                        options.Usings.Add(usingText);
                    }
                }

                var packageReferenceElements = itemGroups.SelectMany(x => x.Descendants("PackageReference"));
                foreach (var packageReferenceElement in packageReferenceElements)
                {
                    var packageIdAttribute = packageReferenceElement.Attribute("Include") ?? packageReferenceElement.Attribute("Update");
                    if (packageIdAttribute is null) continue;
                    var packageId = packageIdAttribute.Value;
                    var packageVersion = packageReferenceElement.Attribute("Version")?.Value;
                    var reference =
                        $"nuget: {packageId}{(string.IsNullOrEmpty(packageVersion) ? "" : $", {packageVersion}")}";
                    options.References.Add(reference);
                }
            }

            var endTime = Stopwatch.GetTimestamp();
            var duration = ProfilerHelper.GetElapsedTime(startTime, endTime);
            _logger.LogDebug("Exact info from project file elapsed time: {duration}", duration);
        }

        // fetch script
        var fetchResult = await _scriptContentFetcher.FetchContent(options);
        if (!fetchResult.IsSuccess())
        {
            _logger.LogError(fetchResult.Msg);
            return -1;
        }
        _logger.LogDebug("ExecutorType: {ExecutorType} References: {References}, Usings: {Usings}",
            options.ExecutorType, 
            options.References.StringJoin(";"),
            options.Usings.StringJoin(";"));

        // compile assembly
        var sourceText = fetchResult.Data;
        var compiler = _compilerFactory.GetCompiler(options.CompilerType);
        var compileStartTime = Stopwatch.GetTimestamp();
        var compileResult = await compiler.Compile(options, sourceText);
        var compileEndTime = Stopwatch.GetTimestamp();
        var compileElapsed = ProfilerHelper.GetElapsedTime(compileStartTime, compileEndTime);
        _logger.LogDebug("Compile elapsed: {elapsed}", compileElapsed);

        if (!compileResult.IsSuccess())
        {
            _logger.LogError($"Compile error:{Environment.NewLine}{compileResult.Msg}");
            return -2;
        }

        Guard.NotNull(compileResult.Data);
        // execute
        var executor = _executorFactory.GetExecutor(options.ExecutorType);
        try
        {
            var executeStartTime = Stopwatch.GetTimestamp();
            var executeResult = await executor.Execute(compileResult.Data, options);
            if (!executeResult.IsSuccess())
            {
                _logger.LogError($"Execute error:{Environment.NewLine}{executeResult.Msg}");
                return -3;
            }
            var executeEndTime = Stopwatch.GetTimestamp();
            var elapsed = ProfilerHelper.GetElapsedTime(executeStartTime, executeEndTime);
            _logger.LogDebug("Execute elapsed: {elapsed}", elapsed);

            // wait for console flush
            await Console.Out.FlushAsync();

            return 0;
        }
        catch (OperationCanceledException) when (options.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Cancelled...");
            return -998;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execute code exception");
            return -999;
        }
    }
}
