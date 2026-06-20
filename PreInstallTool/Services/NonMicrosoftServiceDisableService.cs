using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

/// <summary>
/// Disables non-Microsoft Windows services, matching msconfig "Hide all Microsoft services" + "Disable all".
/// </summary>
public static class NonMicrosoftServiceDisableService
{
    private const int ServiceWin32OwnProcess = 0x10;
    private const int ServiceWin32ShareProcess = 0x20;
    private const int ServiceDisabled = 4;

    private static readonly string SystemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static readonly string[] MicrosoftPathPrefixes =
    [
        SystemRoot,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            "Microsoft Shared")
    ];

    /// <summary>
    /// Services that must stay enabled for immediate reboot/network stability.
    /// </summary>
    public static readonly string[] NeverDisableServices =
    [
        "DcomLaunch",
        "RpcSs",
        "LanmanWorkstation",
        "Dhcp",
        "Dnscache",
        "BFE",
        "mpssvc",
        "WinDefend",
        "EventLog"
    ];

  private static readonly HashSet<string> NeverDisableSet =
        new(NeverDisableServices, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RiotVanguardServiceNames =
        new(
            [
                ..RiotVanguardService.DefaultVanguardServices,
                ..RiotVanguardService.DefaultRiotServices
            ],
            StringComparer.OrdinalIgnoreCase);

    public sealed class DisableResult
    {
        public int DisabledCount { get; init; }
        public int FailedCount { get; init; }
        public int SkippedMicrosoft { get; init; }
        public int SkippedExcluded { get; init; }
        public int AlreadyDisabled { get; init; }
        public IReadOnlyList<string> DisabledServices { get; init; } = [];
    }

    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static DisableResult DisableAllNonMicrosoftServices(IProgress<string>? log)
    {
        if (!IsRunningAsAdministrator())
        {
            throw new InvalidOperationException(LocalizationService.Get("NonMsService_NotAdmin"));
        }

        log?.Report(LocalizationService.Get("NonMsService_Starting"));

        var disabled = new List<string>();
        var failed = 0;
        var skippedMicrosoft = 0;
        var skippedExcluded = 0;
        var alreadyDisabled = 0;

        using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (servicesKey is null)
        {
            log?.Report(LocalizationService.Get("NonMsService_RegistryOpenFailed"));
            return new DisableResult { FailedCount = 1 };
        }

        foreach (var serviceName in servicesKey.GetSubKeyNames().OrderBy(static n => n, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var serviceKey = servicesKey.OpenSubKey(serviceName);
                if (serviceKey is null)
                {
                    continue;
                }

                if (!IsUserVisibleWin32Service(serviceKey))
                {
                    continue;
                }

                if (IsExcludedService(serviceName))
                {
                    skippedExcluded++;
                    log?.Report(LocalizationService.Format("NonMsService_SkippedExcluded", serviceName));
                    continue;
                }

                if (IsMicrosoftService(serviceName, serviceKey))
                {
                    skippedMicrosoft++;
                    continue;
                }

                var start = serviceKey.GetValue("Start");
                if (start is int startValue && startValue == ServiceDisabled)
                {
                    alreadyDisabled++;
                    continue;
                }

                log?.Report(LocalizationService.Format("NonMsService_Disabling", serviceName));
                if (TryDisableService(serviceName, log))
                {
                    disabled.Add(serviceName);
                    log?.Report(LocalizationService.Format("NonMsService_Disabled", serviceName));
                }
                else
                {
                    failed++;
                    log?.Report(LocalizationService.Format("NonMsService_DisableFailed", serviceName));
                }
            }
            catch (Exception ex)
            {
                failed++;
                log?.Report(LocalizationService.Format("NonMsService_DisableFailedDetail", serviceName, ex.Message));
            }
        }

        log?.Report(LocalizationService.Format(
            "NonMsService_Summary",
            disabled.Count,
            skippedMicrosoft,
            skippedExcluded,
            alreadyDisabled,
            failed));

        foreach (var name in disabled)
        {
            log?.Report(LocalizationService.Format("NonMsService_DisabledEntry", name));
        }

        return new DisableResult
        {
            DisabledCount = disabled.Count,
            FailedCount = failed,
            SkippedMicrosoft = skippedMicrosoft,
            SkippedExcluded = skippedExcluded,
            AlreadyDisabled = alreadyDisabled,
            DisabledServices = disabled
        };
    }

    private static bool IsUserVisibleWin32Service(RegistryKey serviceKey)
    {
        var type = serviceKey.GetValue("Type");
        if (type is not int typeValue)
        {
            return false;
        }

        return (typeValue & ServiceWin32OwnProcess) != 0 ||
               (typeValue & ServiceWin32ShareProcess) != 0;
    }

    private static bool IsExcludedService(string serviceName)
    {
        if (NeverDisableSet.Contains(serviceName))
        {
            return true;
        }

        if (RiotVanguardServiceNames.Contains(serviceName))
        {
            return true;
        }

        if (serviceName.StartsWith("Riot", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (serviceName.Contains("Vanguard", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (serviceName.Contains("PreInstallTool", StringComparison.OrdinalIgnoreCase) ||
            serviceName.Contains("UNKCLUB", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsMicrosoftService(string serviceName, RegistryKey serviceKey)
    {
        var imagePath = serviceKey.GetValue("ImagePath") as string;
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            var executable = ExtractExecutablePath(ExpandServicePath(imagePath));
            if (!string.IsNullOrWhiteSpace(executable) && IsUnderMicrosoftPath(executable))
            {
                return true;
            }
        }

        using var parametersKey = serviceKey.OpenSubKey("Parameters");
        if (parametersKey is not null)
        {
            var serviceDll = parametersKey.GetValue("ServiceDll") as string;
            if (!string.IsNullOrWhiteSpace(serviceDll))
            {
                var dllPath = ExpandServicePath(serviceDll);
                if (IsUnderMicrosoftPath(dllPath))
                {
                    return true;
                }
            }
        }

        return IsKnownMicrosoftServiceName(serviceName);
    }

    private static bool IsKnownMicrosoftServiceName(string serviceName) =>
        serviceName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderMicrosoftPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim('"'));
            foreach (var prefix in MicrosoftPathPrefixes)
            {
                if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            var normalized = path.Replace('\\', '/').ToLowerInvariant();
            if (normalized.Contains("/windows/", StringComparison.Ordinal) ||
                normalized.Contains("systemroot", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExpandServicePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (expanded.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            expanded = Path.Combine(SystemRoot, expanded["\\SystemRoot\\".Length..]);
        }
        else if (expanded.StartsWith(@"\SystemRoot", StringComparison.OrdinalIgnoreCase))
        {
            expanded = Path.Combine(SystemRoot, expanded["\\SystemRoot".Length..].TrimStart('\\', '/'));
        }

        return expanded.Trim('"');
    }

    private static string ExtractExecutablePath(string imagePath)
    {
        var trimmed = imagePath.Trim();
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 0)
            {
                return trimmed[1..endQuote];
            }
        }

        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;
    }

    private static bool TryDisableService(string serviceName, IProgress<string>? log)
    {
        RiotVanguardService.TryStopService(serviceName, log);

        var result = SystemChecks.RunProcess(
            "sc.exe",
            $"config {serviceName} start= disabled",
            null,
            [0, 1072, 1060],
            60);

        var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            log?.Report(output);
        }

        return result.Success;
    }
}
