using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.Json;

namespace PreInstallTool.Localization;

public static class LocalizationService
{
    private const string DefaultCulture = "tr";
    private const string SettingsFileName = "language-settings.json";
    private static readonly ResourceManager ResourceManager = new(
        "PreInstallTool.Resources.Strings",
        Assembly.GetExecutingAssembly());

    private static CultureInfo _currentCulture = new(DefaultCulture);

    public static event EventHandler? LanguageChanged;

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("tr", "Türkçe"),
        new("en", "English"),
        new("fr", "Français"),
        new("pt-BR", "Português (Brasil)"),
        new("zh-CN", "中文 (简体)"),
        new("ko", "한국어"),
        new("de", "Deutsch")
    ];

    public static CultureInfo CurrentCulture => _currentCulture;

    public static string CurrentCultureName => _currentCulture.Name;

    public static void Initialize()
    {
        var saved = LoadSavedCulture();
        SetLanguage(saved ?? DefaultCulture, persist: false);
    }

    public static string Get(string key) => GetString(key);

    public static string GetString(string key)
    {
        var value = ResourceManager.GetString(key, _currentCulture);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = ResourceManager.GetString(key, CultureInfo.InvariantCulture);
        return value ?? key;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CurrentCulture, Get(key), args);

    public static void SetLanguage(string cultureName, bool persist = true)
    {
        var normalized = NormalizeCultureName(cultureName);
        if (!AvailableLanguages.Any(l => l.CultureName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            normalized = DefaultCulture;
        }

        var culture = new CultureInfo(normalized);
        _currentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (persist)
        {
            SaveCulture(normalized);
        }

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string? GetLocalizedModeName(string modeId, string fallback) =>
        TryGet($"Mode_{modeId}_Name", fallback);

    public static string? GetLocalizedModeDescription(string modeId, string fallback) =>
        TryGet($"Mode_{modeId}_Description", fallback);

    public static string GetLocalizedStepName(string stepId, string fallback) =>
        TryGet($"Step_{stepId}_Name", fallback) ?? fallback;

    public static string GetLocalizedStepDescription(string stepId, string? fallback) =>
        TryGet($"Step_{stepId}_Description", fallback) ?? fallback ?? string.Empty;

    public static string GetLocalizedFeatureName(string featureId) =>
        Get($"Feature_{featureId}");

    private static string? TryGet(string key, string? fallback)
    {
        var value = ResourceManager.GetString(key, _currentCulture);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = ResourceManager.GetString(key, CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static string NormalizeCultureName(string cultureName) =>
        cultureName switch
        {
            "zh-Hans" or "zh" => "zh-CN",
            _ => cultureName
        };

    private static string? LoadSavedCulture()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<LanguageSettings>(json);
            return string.IsNullOrWhiteSpace(settings?.Language) ? null : settings.Language;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCulture(string cultureName)
    {
        try
        {
            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(new LanguageSettings { Language = cultureName });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Persistence is best-effort.
        }
    }

    private static string GetSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PreInstallTool",
            SettingsFileName);

    private sealed class LanguageSettings
    {
        public string? Language { get; set; }
    }
}
