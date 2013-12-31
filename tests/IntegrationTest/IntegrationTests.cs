﻿// Copyright (c) Weihan Li. All rights reserved.
// Licensed under the MIT license.

using Exec;

namespace IntegrationTest;

public class IntegrationTests
{
    private readonly CommandHandler _handler;

    public IntegrationTests(CommandHandler handler)
    {
        _handler = handler;
    }

    [Theory]
    [InlineData(nameof(RandomSharedSample))]
    public async Task RandomSharedSample(string sampleFileName)
    {
        var filePath = $"{sampleFileName}.cs";
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "CodeSamples", filePath);
        Assert.True(File.Exists(fullPath));

        var execOptions = new ExecOptions()
        {
            ScriptFile = fullPath
        };

        using var output = await ConsoleOutput.CaptureAsync();
        var result = await _handler.Execute(execOptions);        
        Assert.Equal(0, result);
        Assert.NotNull(output.StandardOutput);
        Assert.NotEmpty(output.StandardOutput);
    }
}
