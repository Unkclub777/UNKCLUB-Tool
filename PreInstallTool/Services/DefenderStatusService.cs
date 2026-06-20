using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class DefenderStatusService
{
    private static readonly string[] DefenderServices =
    [
        "WinDefend", "WdFilter", "WdNisDrv", "WdNisSvc"
    ];

    public static IReadOnlyList<FeatureStatusItem> ReadAllFeatures()
    {
        var mpStatus = ReadMpStatus();
        var items = new List<FeatureStatusItem>
        {
            new()
            {
                Id = "defender-service",
                IsEnabled = IsDefenderServiceActive()
            },
            new()
            {
                Id = "real-time-protection",
                IsEnabled = ReadRealTimeProtection(mpStatus)
            },
            new()
            {
                Id = "behavior-monitoring",
                IsEnabled = ReadBehaviorMonitoring(mpStatus)
            },
            new()
            {
                Id = "network-protection",
                IsEnabled = ReadNetworkProtection(mpStatus)
            },
            new()
            {
                Id = "cloud-protection",
                IsEnabled = ReadCloudProtection(mpStatus)
            },
            new()
            {
                Id = "pua-protection",
                IsEnabled = ReadPuaProtection(mpStatus)
            },
            new()
            {
                Id = "tamper-protection",
                IsEnabled = ReadTamperProtection()
            },
            new()
            {
                Id = "smartscreen-app",
                IsEnabled = ReadAppSmartScreen()
            },
            new()
            {
                Id = "edge-smartscreen",
                IsEnabled = ReadEdgeSmartScreen()
            }
        };

        return items;
    }

    public static bool AnyFeatureEnabled() =>
        ReadAllFeatures().Any(static feature => feature.IsEnabled);

    public static IReadOnlyList<string> GetEnabledFeatureIds() =>
        ReadAllFeatures()
            .Where(static feature => feature.IsEnabled)
            .Select(static feature => feature.Id)
            .ToList();

    private static MpStatusSnapshot ReadMpStatus()
    {
        try
        {
            var script = """
                $ErrorActionPreference = 'SilentlyContinue'
                $status = $null
                $pref = $null
                if (Get-Command Get-MpComputerStatus -ErrorAction SilentlyContinue) {
                    $status = Get-MpComputerStatus | Select-Object RealTimeProtectionEnabled, AntivirusEnabled, IsTamperProtected
                }
                if (Get-Command Get-MpPreference -ErrorAction SilentlyContinue) {
                    $pref = Get-MpPreference | Select-Object DisableBehaviorMonitoring, EnableNetworkProtection, MAPSReporting, PUAProtection
                }
                [PSCustomObject]@{
                    Status = $status
                    Preference = $pref
                } | ConvertTo-Json -Compress -Depth 4
                """;

            var startInfo = new ProcessStartInfo
            {
                FileName = SystemChecks.ResolveCommandPath("powershell.exe"),
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "`\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return MpStatusSnapshot.Empty;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // ignored
                }

                return MpStatusSnapshot.Empty;
            }

            var json = process.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                return MpStatusSnapshot.Empty;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            bool? realTime = null;
            bool? behaviorDisabled = null;
            int? networkProtection = null;
            int? mapsReporting = null;
            int? puaProtection = null;
            bool? tamperProtected = null;

            if (root.TryGetProperty("Status", out var statusElement) &&
                statusElement.ValueKind == JsonValueKind.Object)
            {
                realTime = ReadNullableBool(statusElement, "RealTimeProtectionEnabled");
                tamperProtected = ReadNullableBool(statusElement, "IsTamperProtected");
            }

            if (root.TryGetProperty("Preference", out var prefElement) &&
                prefElement.ValueKind == JsonValueKind.Object)
            {
                behaviorDisabled = ReadNullableBool(prefElement, "DisableBehaviorMonitoring");
                networkProtection = ReadNullableInt(prefElement, "EnableNetworkProtection");
                mapsReporting = ReadNullableInt(prefElement, "MAPSReporting");
                puaProtection = ReadNullableInt(prefElement, "PUAProtection");
            }

            return new MpStatusSnapshot(
                realTime,
                behaviorDisabled,
                networkProtection,
                mapsReporting,
                puaProtection,
                tamperProtected);
        }
        catch
        {
            return MpStatusSnapshot.Empty;
        }
    }

    private static bool IsDefenderServiceActive()
    {
        if (ReadRegistryDword("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows Defender", "DisableAntiSpyware") == 1)
        {
            return false;
        }

        foreach (var serviceName in DefenderServices)
        {
            if (IsServiceRunning(serviceName))
            {
                return true;
            }
        }

        return ReadRegistryDword($"HKLM\\SYSTEM\\CurrentControlSet\\Services\\WinDefend", "Start") is not 4;
    }

    private static bool ReadRealTimeProtection(MpStatusSnapshot mpStatus)
    {
        if (mpStatus.RealTimeProtectionEnabled.HasValue)
        {
            return mpStatus.RealTimeProtectionEnabled.Value;
        }

        if (ReadRegistryDword(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                "DisableRealtimeMonitoring") == 1)
        {
            return false;
        }

        return ReadRegistryDword(
                   @"HKLM\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection",
                   "DisableRealtimeMonitoring") != 1;
    }

    private static bool ReadBehaviorMonitoring(MpStatusSnapshot mpStatus)
    {
        if (mpStatus.DisableBehaviorMonitoring.HasValue)
        {
            return !mpStatus.DisableBehaviorMonitoring.Value;
        }

        return ReadRegistryDword(
                   @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                   "DisableBehaviorMonitoring") != 1;
    }

    private static bool ReadNetworkProtection(MpStatusSnapshot mpStatus)
    {
        if (mpStatus.EnableNetworkProtection.HasValue)
        {
            return mpStatus.EnableNetworkProtection.Value is 1 or 2;
        }

        return ReadRegistryDword(
                   @"HKLM\SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Network Protection",
                   "EnableNetworkProtection") is 1 or 2;
    }

    private static bool ReadCloudProtection(MpStatusSnapshot mpStatus)
    {
        if (mpStatus.MapsReporting.HasValue)
        {
            return mpStatus.MapsReporting.Value is 1 or 2;
        }

        return ReadRegistryDword(
                   @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet",
                   "SpynetReporting") is 1 or 2;
    }

    private static bool ReadPuaProtection(MpStatusSnapshot mpStatus)
    {
        if (mpStatus.PuaProtection.HasValue)
        {
            return mpStatus.PuaProtection.Value is 1 or 2;
        }

        return ReadRegistryDword(
                   @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\MpEngine",
                   "MpEnablePus") == 1;
    }

    private static bool ReadTamperProtection()
    {
        var policy = ReadRegistryDword(
            @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Features",
            "TamperProtection");

        if (policy.HasValue)
        {
            return policy.Value == 1;
        }

        var feature = ReadRegistryDword(
            @"HKLM\SOFTWARE\Microsoft\Windows Defender\Features",
            "TamperProtection");

        return feature == 1;
    }

    private static bool ReadAppSmartScreen()
    {
        var policy = ReadRegistryDword(
            @"HKLM\SOFTWARE\Policies\Microsoft\Windows\System",
            "EnableSmartScreen");

        if (policy.HasValue)
        {
            return policy.Value == 1;
        }

        var explorer = ReadRegistryString(
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
            "SmartScreenEnabled");

        if (!string.IsNullOrWhiteSpace(explorer))
        {
            return !explorer.Equals("Off", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool ReadEdgeSmartScreen()
    {
        var edge = ReadRegistryDword(
            @"HKLM\SOFTWARE\Policies\Microsoft\Edge",
            "SmartScreenEnabled");

        if (edge.HasValue)
        {
            return edge.Value == 1;
        }

        var legacy = ReadRegistryDword(
            @"HKLM\SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter",
            "EnabledV9");

        if (legacy.HasValue)
        {
            return legacy.Value == 1;
        }

        return true;
    }

    private static bool IsServiceRunning(string serviceName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = SystemChecks.ResolveCommandPath("sc.exe"),
                Arguments = $"query \"{serviceName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // ignored
                }

                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int? ReadRegistryDword(string keyPath, string valueName)
    {
        try
        {
            var (hive, subKey) = ParseRegistryKey(keyPath);
            using var key = hive.OpenSubKey(subKey);
            var value = key?.GetValue(valueName);
            return value switch
            {
                int intValue => intValue,
                byte byteValue => byteValue,
                _ when value is not null && int.TryParse(value.ToString(), out var parsed) => parsed,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadRegistryString(string keyPath, string valueName)
    {
        try
        {
            var (hive, subKey) = ParseRegistryKey(keyPath);
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool? ReadNullableBool(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var number) => number != 0,
            _ => null
        };
    }

    private static int? ReadNullableInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return number;
        }

        return null;
    }

    private static (RegistryKey hive, string subKey) ParseRegistryKey(string keyPath)
    {
        var parts = keyPath.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Geçersiz registry anahtarı: {keyPath}");
        }

        var hive = parts[0].ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            _ => throw new InvalidOperationException($"Desteklenmeyen registry hive: {parts[0]}")
        };

        return (hive, parts[1]);
    }

    private readonly record struct MpStatusSnapshot(
        bool? RealTimeProtectionEnabled,
        bool? DisableBehaviorMonitoring,
        int? EnableNetworkProtection,
        int? MapsReporting,
        int? PuaProtection,
        bool? IsTamperProtected)
    {
        public static MpStatusSnapshot Empty { get; } = new(null, null, null, null, null, null);
    }
}
