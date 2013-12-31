﻿// Copyright (c) 2022-2023 Weihan Li. All rights reserved.
// Licensed under the Apache license version 2.0 http://www.apache.org/licenses/LICENSE-2.0

using Exec.Contracts;
using System.Text.Json;

namespace Exec.Services;

public sealed class ConfigProfileManager : IConfigProfileManager
{
    private static readonly string ProfileFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet",
        "tools",
        Helper.ApplicationName,
        "profiles"
    );

    static ConfigProfileManager()
    {
        if (!Directory.Exists(ProfileFolder))
        {
            Directory.CreateDirectory(ProfileFolder);
        }
    }

    public async Task ConfigureProfile(string profileName, ConfigProfile profile)
    {
        var profilePath = Path.Combine(ProfileFolder, $"{profileName}.json");
        await using var fs = File.OpenWrite(profilePath);
        await JsonSerializer.SerializeAsync(fs, profile, JsonSerializerOptionsHelper.WriteIndented);
    }

    public Task DeleteProfile(string profileName)
    {
        var profilePath = Path.Combine(ProfileFolder, $"{profileName}.json");
        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }
        return Task.CompletedTask;
    }

    public async Task<ConfigProfile?> GetProfile(string profileName)
    {
        var profilePath = Path.Combine(ProfileFolder, $"{profileName}.json");
        if (!File.Exists(profilePath)) return null;

        await using var fs = File.OpenRead(profilePath);
        return await JsonSerializer.DeserializeAsync<ConfigProfile>(fs);
    }

    public Task<string[]> ListProfiles()
    {
        var profileNames = Directory.GetFiles(ProfileFolder, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .WhereNotNull()
            .ToArray();
        return profileNames.WrapTask();
    }
}
