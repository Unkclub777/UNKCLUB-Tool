using System.IO;
using System.Text.Json;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class ResellerSelectionService
{
    private const string SettingsFileName = "reseller-selection.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static ResellerType _current = Load();

    public static ResellerType Current
    {
        get => _current;
        set
        {
            if (_current == value)
            {
                return;
            }

            _current = value;
            Save(value);
        }
    }

    public static bool IsUnkclub => Current == ResellerType.Unkclub;

    public static bool IsOtherReseller => Current == ResellerType.OtherReseller;

    private static ResellerType Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return ResellerType.Unkclub;
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<ResellerSettings>(json, JsonOptions);
            return settings?.Reseller ?? ResellerType.Unkclub;
        }
        catch
        {
            return ResellerType.Unkclub;
        }
    }

    private static void Save(ResellerType reseller)
    {
        try
        {
            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(new ResellerSettings { Reseller = reseller }, JsonOptions);
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

    private sealed class ResellerSettings
    {
        public ResellerType Reseller { get; set; } = ResellerType.Unkclub;
    }
}
