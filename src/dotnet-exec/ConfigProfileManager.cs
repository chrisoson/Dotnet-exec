﻿// Copyright (c) Weihan Li. All rights reserved.
// Licensed under the MIT license.

using System.Text.Json;

namespace Exec;

public interface IConfigProfileManager
{
    Task ConfigureProfile(string profileName, ConfigProfile profile);

    Task DeleteProfile(string profileName);

    Task<ConfigProfile?> GetProfile(string profileName);
}

public sealed class ConfigProfileManager: IConfigProfileManager
{
    private static readonly string ProfileFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet",
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
        await JsonSerializer.SerializeAsync(fs, profile, new JsonSerializerOptions()
        {
            WriteIndented = true
        });
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
}
