using System.IO;
using System.Text.Json;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static InstallConfig Load(string? configPath = null)
    {
        AppResourceService.EnsureInitialized();
        configPath ??= AppResourceService.GetPath("install-config.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                LocalizationService.Format("ConfigFileNotFound", configPath),
                configPath);
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<InstallConfig>(json, JsonOptions)
                     ?? throw new InvalidOperationException(LocalizationService.Get("ConfigFileUnreadable"));

        if (config.Modes.Count == 0 && config.Steps.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.Get("ConfigNoModesOrSteps"));
        }

        return config;
    }
}
