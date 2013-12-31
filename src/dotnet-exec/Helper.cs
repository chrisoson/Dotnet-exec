﻿// Copyright (c) Weihan Li. All rights reserved.
// Licensed under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NuGet.Versioning;
using ReferenceResolver;
using System.Reflection;
using System.Text;
using WeihanLi.Common.Models;

namespace Exec;

public static class Helper
{
    private static readonly HashSet<string> SpecialConsoleDiagnosticIds = new() { "CS5001", "CS0028" };

    public const string ApplicationName = "dotnet-exec";

    public const string Default = "default";

    public const string Script = "script";

    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services, string[] args)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(args.Contains("--debug") ? LogLevel.Debug : LogLevel.Error);
        });
        services.AddSingleton(sp => sp.GetRequiredService<ILoggerFactory>()
            .CreateLogger(ApplicationName));
        services.AddSingleton<DefaultCodeCompiler>();
        services.AddSingleton<WorkspaceCodeCompiler>();
        services.AddSingleton<AdvancedCodeCompiler>();
        services.AddSingleton<CSharpScriptCompilerExecutor>();
        services.AddSingleton<ICompilerFactory, CompilerFactory>();
        services.AddSingleton<DefaultCodeExecutor>();
        services.AddSingleton<IExecutorFactory, ExecutorFactory>();
        services.AddSingleton<CommandHandler>();
        services.AddSingleton<ICommandHandler>(sp => sp.GetRequiredService<CommandHandler>());
        services.AddSingleton<IScriptContentFetcher, ScriptContentFetcher>();
        services.AddSingleton<IAdditionalScriptContentFetcher, AdditionalScriptContentFetcher>();
        services.AddHttpClient(nameof(ScriptContentFetcher));
        services.AddReferenceResolvers();
        services.AddSingleton<IRefResolver, RefResolver>();

        return services;
    }

    public static async Task<Result<CompileResult>> GetCompilationAssemblyResult(this Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        var result = await GetCompilationResult(compilation, cancellationToken);
        if (result.EmitResult.Success)
        {
            Guard.NotNull(result.Assembly).Seek(0, SeekOrigin.Begin);
            return Result.Success(new CompileResult(result.Compilation, result.EmitResult,
                result.Assembly));
        }

        var error = new StringBuilder();
        foreach (var diagnostic in result.EmitResult.Diagnostics)
        {
            var message = CSharpDiagnosticFormatter.Instance.Format(diagnostic);
            error.AppendLine($"{diagnostic.Id}-{diagnostic.Severity}-{message}");
        }

        return Result.Fail<CompileResult>(error.ToString(), ResultStatus.ProcessFail);
    }

    private static async Task<(Compilation Compilation, EmitResult EmitResult, MemoryStream? Assembly)>
        GetCompilationResult(Compilation compilation, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);
        if (emitResult.Success)
        {
            return (compilation, emitResult, ms);
        }

        if (emitResult.Diagnostics.Any(d => SpecialConsoleDiagnosticIds.Contains(d.Id)))
        {
            ms.Seek(0, SeekOrigin.Begin);
            ms.SetLength(0);

            var options = compilation.Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary);
            emitResult = compilation.WithOptions(options)
                .Emit(ms, cancellationToken: cancellationToken);
            return (compilation, emitResult, emitResult.Success ? ms : null);
        }

        return (compilation, emitResult, null);
    }

    // https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview#implicit-using-directives
    private static IEnumerable<string> GetGlobalUsingsInternal(ExecOptions options)
    {
        // Default SDK
        yield return "System";
        yield return "System.Collections.Generic";
        yield return "System.IO";
        yield return "System.Linq";
        yield return "System.Net.Http";
        yield return "System.Text";
        yield return "System.Threading";
        yield return "System.Threading.Tasks";

        if (!options.IsScriptExecutor())
        {
            // Web
            yield return "System.Net.Http.Json";
            yield return "Microsoft.AspNetCore.Builder";
            yield return "Microsoft.AspNetCore.Hosting";
            yield return "Microsoft.AspNetCore.Http";
            yield return "Microsoft.AspNetCore.Routing";
            yield return "Microsoft.Extensions.Configuration";
            yield return "Microsoft.Extensions.DependencyInjection";
            yield return "Microsoft.Extensions.Hosting";
            yield return "Microsoft.Extensions.Logging";
        }


        if (options.IncludeWideReferences)
        {
            yield return "WeihanLi.Common";
            yield return "WeihanLi.Common.Logging";
            yield return "WeihanLi.Common.Helpers";
            yield return "WeihanLi.Extensions";
            yield return "WeihanLi.Extensions.Dump";
        }
    }

    public static HashSet<string> GetGlobalUsings(ExecOptions options)
    {
        var usings = new HashSet<string>(GetGlobalUsingsInternal(options));
        if (options.Usings.HasValue())
        {
            foreach (var @using in options.Usings)
            {
                if (@using.StartsWith('-'))
                {
                    usings.Remove(@using[1..]);
                }
                else
                {
                    usings.Add(@using);
                }
            }
        }
        return usings;
    }

    public static string GetGlobalUsingsCodeText(ExecOptions options)
    {
        var usings = GetGlobalUsings(options);

        var usingText = usings.Select(x => $"global using {x};").StringJoin(Environment.NewLine);
        if (options.LanguageVersion != LanguageVersion.Preview)
            return usingText;
        // Generate System.Runtime.Versioning.RequiresPreviewFeatures attribute on assembly level
        return $"{usingText}{Environment.NewLine}[assembly:System.Runtime.Versioning.RequiresPreviewFeatures]";
    }

    private static void LoadSupportedFrameworks()
    {
        var frameworkDir = Path.Combine(FrameworkReferenceResolver.DotnetDirectory, "shared", FrameworkReferenceResolver.FrameworkNames.Default);
        foreach (var framework in Directory
                     .GetDirectories(frameworkDir)
                     .Select(Path.GetFileName)
                     .WhereNotNull()
                     .Where(x => x.Length > 0 && char.IsDigit(x[0]))
                 )
        {
            if (NuGetVersion.TryParse(framework, out var frameworkVersion)
                && frameworkVersion.Major >= 6)
            {
                _supportedFrameworks.Add($"net{frameworkVersion.Major}.{frameworkVersion.Minor}");
            }
        }
    }

    // ReSharper disable once InconsistentNaming
    private static readonly HashSet<string> _supportedFrameworks = new();
    public static HashSet<string> SupportedFrameworks
    {
        get
        {
            if (_supportedFrameworks.Count == 0)
            {
                LoadSupportedFrameworks();
            }
            return _supportedFrameworks;
        }
    }

    public static string GetReferencePackageName(string frameworkName)
    {
        return frameworkName switch
        {
            FrameworkReferenceResolver.FrameworkNames.Web => FrameworkReferencePackages.Web,
            FrameworkReferenceResolver.FrameworkNames.WindowsDesktop => FrameworkReferencePackages.WindowsDesktop,
            _ => FrameworkReferencePackages.Default
        };
    }

    public static IEnumerable<string> GetDependencyFrameworks(ExecOptions options)
    {
        yield return FrameworkReferenceResolver.FrameworkNames.Default;
        if (!options.IsScriptExecutor())
        {
            yield return FrameworkReferenceResolver.FrameworkNames.Web;
        }
    }
    
    public static void EnableReferencesSupersedeLowerVersions(this CompilationOptions compilationOptions)
    {
        // https://github.com/dotnet/roslyn/blob/a51b65c86bb0f42a79c47798c10ad75d5c343f92/src/Compilers/Core/Portable/Compilation/CompilationOptions.cs#L183
        typeof(CompilationOptions)
            .GetProperty("ReferencesSupersedeLowerVersions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetMethod!
            .Invoke(compilationOptions, new object[] { true });
    }

    private static bool IsScriptExecutor(this ExecOptions options) => Script.EqualsIgnoreCase(options.ExecutorType);
}

internal static class FrameworkReferencePackages
{
    public const string Default = "Microsoft.NETCore.App.Ref";
    public const string Web = "Microsoft.AspNetCore.App.Ref";
    public const string WindowsDesktop = "Microsoft.WindowsDesktop.App.Ref";
}
