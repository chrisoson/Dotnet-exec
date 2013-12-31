﻿// Copyright (c) 2022-2024 Weihan Li. All rights reserved.
// Licensed under the Apache license version 2.0 http://www.apache.org/licenses/LICENSE-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WeihanLi.Common.Models;

namespace Exec.Services;

public interface IRepl
{
    Task RunAsync(ExecOptions options);
}

[ExcludeFromCodeCoverage]
internal sealed class Repl
    (
        IRefResolver referenceResolver,
        IAdditionalScriptContentFetcher scriptContentFetcher
    ) : IRepl
{
    public async Task RunAsync(ExecOptions options)
    {
        var references = await referenceResolver.ResolveMetadataReferences(options, false);
        var globalUsings = Helper.GetGlobalUsingList(options);
        var scriptOptions = ScriptOptions.Default
                .WithReferences(references)
                .WithOptimizationLevel(options.Configuration)
                .WithAllowUnsafe(true)
                .WithLanguageVersion(options.GetLanguageVersion())
                .AddImports(globalUsings.Select(g => g.TrimStart("global::")))
            ;

        ScriptState state = await CSharpScript.RunAsync("""Console.WriteLine("REPL started, Enter #exit to exit, #help for help text");""", scriptOptions);
        if (options.AdditionalScripts.HasValue())
        {
            foreach (var additionalScript in options.AdditionalScripts)
            {
                var additionalScriptCode = await scriptContentFetcher.FetchContent(additionalScript, options.CancellationToken);
                if (additionalScriptCode.IsSuccess())
                {
                    state = await state.ContinueWithAsync(additionalScriptCode.Data, scriptOptions);
                }
            }
        }

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
                continue;

            if ("#exit".EqualsIgnoreCase(input))
                break;

            if ("#help".EqualsIgnoreCase(input))
            {
                // print detailed help text
                continue;
            }

            if (input.StartsWith("#r ", StringComparison.Ordinal))
            {
                try
                {
                    var reference = input[3..];
                    options.References.Add(Helper.ReferenceNormalize(reference));
                    options.DisableCache = true;
                    references = await referenceResolver.ResolveMetadataReferences(options, false);
                    scriptOptions = scriptOptions.WithReferences(references);
                    state = await CSharpScript.RunAsync(state.Script.Code, scriptOptions);
                    ConsoleHelper.WriteLineWithColor("Reference added", ConsoleColor.DarkGreen);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLineWithColor($"Exception when add reference", ConsoleColor.DarkRed);
                    ConsoleHelper.WriteLineWithColor(CSharpObjectFormatter.Instance.FormatException(ex), ConsoleColor.DarkRed);
                }
                continue;
            }

            if (input.EndsWith('.'))
            {
                var completions = await GetCompletions(state, scriptOptions, input, options);
                if (completions is { Count: > 0 })
                {
                    foreach (var completion in completions)
                    {
                        Console.WriteLine(completion.DisplayText);
                    }
                }
                continue;
            }

            try
            {
                var anotherScriptState = await state.ContinueWithAsync(input, scriptOptions);
                var diagnostics = anotherScriptState.Script.Compile();
                if (diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
                {
                    // error
                    foreach (var diagnostic in diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Error))
                    {
                        ConsoleHelper.WriteLineWithColor(CSharpDiagnosticFormatter.Instance.Format(diagnostic, CultureInfo.CurrentCulture), ConsoleColor.DarkRed);
                    }
                    continue;
                }

                try
                {
                    var anotherState = await anotherScriptState.Script.RunFromAsync(state);
                    if (anotherState.ReturnValue is not null)
                    {
                        Console.WriteLine(CSharpObjectFormatter.Instance.FormatObject(anotherState.ReturnValue));
                    }
                    state = anotherState;
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLineWithColor($"Exception when execute script", ConsoleColor.DarkRed);
                    ConsoleHelper.WriteLineWithColor(CSharpObjectFormatter.Instance.FormatException(ex), ConsoleColor.DarkRed);
                }
            }
            catch (CompilationErrorException e)
            {
                ConsoleHelper.WriteLineWithColor($"Exception when compile script", ConsoleColor.DarkRed);
                ConsoleHelper.WriteLineWithColor(CSharpObjectFormatter.Instance.FormatException(e), ConsoleColor.DarkRed);
                foreach (var diagnostic in e.Diagnostics)
                {
                    ConsoleHelper.WriteLineWithColor(CSharpDiagnosticFormatter.Instance.Format(diagnostic, CultureInfo.CurrentCulture), ConsoleColor.DarkRed);
                }
            }
        }
    }

    private static async Task<IReadOnlyList<CompletionItem>> GetCompletions(
        ScriptState scriptState, ScriptOptions scriptOptions, string input, ExecOptions options)
    {
        // https://www.strathweb.com/2018/12/using-roslyn-c-completion-service-programmatically/
        // https://github.com/filipw/Strathweb.Samples.Roslyn.Completion
        using var workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
    optimizationLevel: options.Configuration, nullableContextOptions: NullableContextOptions.Annotations);

        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "dotnet-exec-repl",
            "dotnet-exec-repl",
            LanguageNames.CSharp,
            isSubmission: true)
            .WithMetadataReferences(scriptOptions.MetadataReferences)
            .WithCompilationOptions(compilationOptions)
            ;
        var project = workspace.AddProject(projectInfo);

        var combinedCode = scriptState.Script.Code + Environment.NewLine + input;
        Debug.WriteLine(scriptState.Script.Code);

        var documentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id), "__Script.cs",
            sourceCodeKind: SourceCodeKind.Script,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(combinedCode), VersionStamp.Default)));
        var document = workspace.AddDocument(documentInfo);

        var completionService = CompletionService.GetService(document);
        if (completionService is null) return [];

        var completionList = await completionService.GetCompletionsAsync(document, combinedCode.Length);
        return completionList.ItemsList;
    }
}
