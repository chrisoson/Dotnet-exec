﻿// Copyright (c) Weihan Li. All rights reserved.
// Licensed under the MIT license.

using WeihanLi.Common.Http;
using WeihanLi.Common.Models;

namespace Exec;

public interface IScriptContentFetcher
{
    Task<Result<string>> FetchContent(ExecOptions options);
}

public interface IAdditionalScriptContentFetcher
{
    Task<Result<string>> FetchContent(string script, CancellationToken cancellationToken = default);
}

public class AdditionalScriptContentFetcher: IAdditionalScriptContentFetcher
{
    // for test only
    internal static IAdditionalScriptContentFetcher InstanceForTest { get; } 
        = new AdditionalScriptContentFetcher(new MockHttpClientFactory(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    private sealed class MockHttpClientFactory: IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new NoProxyHttpClientHandler());
        }
    }


    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public AdditionalScriptContentFetcher(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    public async Task<Result<string>> FetchContent(string script, CancellationToken cancellationToken = default)
    {
        string sourceText;
        try
        {
            if (Uri.TryCreate(script, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                var httpClient = _httpClientFactory.CreateClient(nameof(ScriptContentFetcher));
                var scriptUrl = uri.Host switch
                {
                    "github.com" => script
                        .Replace($"://{uri.Host}/", $"://raw.githubusercontent.com/")
                        .Replace("/blob/", "/")
                        .Replace("/tree/", "/"),
                    "gist.github.com" => script
                                             .Replace($"://{uri.Host}/", $"://gist.githubusercontent.com/")
                                         + "/raw",
                    _ => script
                };
                sourceText = await httpClient.GetStringAsync(scriptUrl, cancellationToken);
            }
            else
            {
                if (!File.Exists(script))
                {
                    _logger.LogError("The file {ScriptFile} does not exists", script);
                    return Result.Fail<string>("File path not exits");
                }

                sourceText = await File.ReadAllTextAsync(script, cancellationToken);
            }
        }
        catch (Exception e)
        {
            return Result.Fail<string>($"Fail to fetch script content, {e}", ResultStatus.ProcessFail);
        }

        return Result.Success<string>(sourceText);
    }
}

public sealed class ScriptContentFetcher : AdditionalScriptContentFetcher, IScriptContentFetcher
{
    public ScriptContentFetcher(IHttpClientFactory httpClientFactory, ILogger logger)
         : base(httpClientFactory, logger)
    {
    }

    public async Task<Result<string>> FetchContent(ExecOptions options)
    {
        var scriptFile = options.Script;
        const string codePrefix = "code:";
        if (scriptFile.StartsWith(codePrefix))
        {
            var code = scriptFile[codePrefix.Length..];
            if (code.EndsWith(".Dump()"))
            {
                // auto fix for `Dump()`
                code = $"{code};";
            }
            return Result.Success<string>(code);
        }

        const string scriptPrefix = "script:";
        if (scriptFile.StartsWith(scriptPrefix))
        {
            var code = scriptFile[scriptPrefix.Length..];
            options.ExecutorType = options.CompilerType = Helper.Script;
            return Result.Success<string>(code);
        }

        var sourceTextResult = await FetchContent(options.Script);
        if (sourceTextResult.Status != ResultStatus.Success)
        {
            return sourceTextResult;
        }

        var sourceText = sourceTextResult.Data;
        Guard.NotNull(sourceText);
        
        var scriptReferences = new HashSet<string>();
        var scriptUsings = new HashSet<string>();

        foreach (var line in sourceText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("//"))
            {
                break;
            }

            // exact reference from file
            if (line.StartsWith("//r:")
                || line.StartsWith("// r:")
                || line.StartsWith("//reference:")
                || line.StartsWith("// reference:")
                       )
            {
                var reference = line.Split(':', 2)[1].Trim();
                if (reference.IsNotNullOrEmpty())
                {
                    scriptReferences.Add(reference);
                }

                continue;
            }

            // exact using from file
            if (line.StartsWith("//u:")
                || line.StartsWith("// u:")
                || line.StartsWith("//using:")
                || line.StartsWith("// using:")
               )
            {
                var @using = line.Split(':', 2)[1].Trim();
                if (@using.IsNotNullOrEmpty())
                {
                    scriptUsings.Add(@using);
                }
            }
        }

        if (scriptReferences.Count > 0)
        {
            if (options.References.HasValue())
            {
                foreach (var reference in options.References)
                {
                    scriptReferences.Add(reference);
                }
            }
            options.References = scriptReferences;
        }
        if (scriptUsings.Count > 0)
        {
            if (options.Usings.HasValue())
            {
                foreach (var @using in options.Usings)
                {
                    scriptUsings.Add(@using);
                }
            }
            options.Usings = scriptUsings;
        }

        return Result.Success<string>(sourceText);
    }
}
