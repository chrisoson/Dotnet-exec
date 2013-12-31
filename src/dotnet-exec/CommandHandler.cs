﻿// Copyright (c) Weihan Li. All rights reserved.
// Licensed under the MIT license.

using System.CommandLine.Invocation;
using System.Text.Json;
using WeihanLi.Common.Models;

namespace Exec;

public sealed class CommandHandler
{
    private readonly ILogger _logger;
    private readonly ICompilerFactory _compilerFactory;
    private readonly ICodeExecutor _executor;
    private readonly IScriptContentFetcher _scriptContentFetcher;

    public CommandHandler(ILogger logger, ICompilerFactory compilerFactory, ICodeExecutor executor,
        IScriptContentFetcher scriptContentFetcher)
    {
        _logger = logger;
        _compilerFactory = compilerFactory;
        _executor = executor;
        _scriptContentFetcher = scriptContentFetcher;
    }

    public async Task<int> InvokeAsync(InvocationContext context)
    {
        var parseResult = context.ParseResult;

        // 1. options binding
        var options = new ExecOptions();
        options.BindCommandLineArguments(parseResult);
        options.CancellationToken = context.GetCancellationToken();

        _logger.LogDebug("options: {options}", JsonSerializer.Serialize(options));

        return await Execute(options);
    }

    public async Task<int> Execute(ExecOptions options)
    {
        if (options.Script.IsNullOrWhiteSpace())
        {
            _logger.LogError("The file {ScriptFile} can not be empty", options.Script);
            return -1;
        }

        // fetch script
        var fetchResult = await _scriptContentFetcher.FetchContent(options.Script, options.CancellationToken);
        if (!fetchResult.IsSuccess())
        {
            _logger.LogError(fetchResult.Msg);
            return -1;
        }

        // compile assembly
        var sourceText = fetchResult.Data;
        var compiler = _compilerFactory.GetCompiler(options.CompilerType);
        var compileResult = await compiler.Compile(options, sourceText);
        if (!compileResult.IsSuccess())
        {
            _logger.LogError($"Compile error:{Environment.NewLine}{compileResult.Msg}");
            return -2;
        }

        Guard.NotNull(compileResult.Data);
        // execute
        try
        {
            var executeResult = await _executor.Execute(compileResult.Data, options);
            if (!executeResult.IsSuccess())
            {
                _logger.LogError($"Execute error:{Environment.NewLine}{executeResult.Msg}");
                return -3;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execute code error");
            return -999;
        }
    }
}
