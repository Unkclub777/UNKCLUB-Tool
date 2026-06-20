using System.IO;
using Microsoft.Win32;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public enum VanguardInstallState
{
    Installed,
    Partial,
    NotInstalled
}

public readonly record struct VanguardDetectionResult(
    VanguardInstallState State,
    bool FolderExists,
    bool VgkServiceInstalled,
    bool VgcServiceInstalled,
    bool RegistryEntryFound);

public static class VanguardDetectionService
{
    private const string VanguardFolder = @"C:\Program Files\Riot Vanguard";
    private const string VanguardRegistryKey = @"SOFTWARE\Riot Vanguard";

    public static VanguardDetectionResult Detect()
    {
        var folderExists = Directory.Exists(VanguardFolder);
        var vgkInstalled = IsServiceInstalled("vgk");
        var vgcInstalled = IsServiceInstalled("vgc");
        var registryFound = RegistryEntryExists();

        var state = ResolveState(folderExists, vgkInstalled, vgcInstalled, registryFound);
        return new VanguardDetectionResult(state, folderExists, vgkInstalled, vgcInstalled, registryFound);
    }

    public static bool IsProperlyInstalled() => Detect().State == VanguardInstallState.Installed;

    public static string BuildStatusSummary(VanguardDetectionResult result) =>
        LocalizationService.Format(
            "Vanguard_DetectionSummary",
            result.FolderExists ? LocalizationService.Get("Vanguard_Yes") : LocalizationService.Get("Vanguard_No"),
            result.VgkServiceInstalled ? LocalizationService.Get("Vanguard_Yes") : LocalizationService.Get("Vanguard_No"),
            result.VgcServiceInstalled ? LocalizationService.Get("Vanguard_Yes") : LocalizationService.Get("Vanguard_No"),
            result.RegistryEntryFound ? LocalizationService.Get("Vanguard_Yes") : LocalizationService.Get("Vanguard_No"));

    private static VanguardInstallState ResolveState(
        bool folderExists,
        bool vgkInstalled,
        bool vgcInstalled,
        bool registryFound)
    {
        var signals = new[] { folderExists, vgkInstalled, vgcInstalled, registryFound };
        var positiveCount = signals.Count(static value => value);

        if (positiveCount >= 3)
        {
            return VanguardInstallState.Installed;
        }

        if (positiveCount == 0)
        {
            return VanguardInstallState.NotInstalled;
        }

        return VanguardInstallState.Partial;
    }

    private static bool IsServiceInstalled(string serviceName) =>
        RiotVanguardService.QueryService(serviceName) is not null;

    private static bool RegistryEntryExists()
    {
        try
        {
            using var localMachine = Registry.LocalMachine.OpenSubKey(VanguardRegistryKey);
            if (localMachine is not null)
            {
                return true;
            }

            using var localMachineWow64 = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\WOW6432Node\{VanguardRegistryKey}");
            return localMachineWow64 is not null;
        }
        catch
        {
            return false;
        }
    }
}
