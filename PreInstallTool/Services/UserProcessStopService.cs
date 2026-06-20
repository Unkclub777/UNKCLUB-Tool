using System.Diagnostics;
using System.IO;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

/// <summary>
/// Stops user-session applications while preserving Windows system processes and this tool.
/// </summary>
public static class UserProcessStopService
{
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "Idle",
        "Registry",
        "smss",
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "lsaiso",
        "svchost",
        "dwm",
        "explorer",
        "fontdrvhost",
        "sihost",
        "taskhostw",
        "RuntimeBroker",
        "SearchHost",
        "StartMenuExperienceHost",
        "ShellExperienceHost",
        "audiodg",
        "conhost",
        "dllhost",
        "WmiPrvSE",
        "MsMpEng",
        "NisSrv",
        "SecurityHealthService",
        "spoolsv",
        "PreInstallTool",
        "UNKCLUB Tool",
        "red",
        "SystemSettings",
        "ApplicationFrameHost",
        "TextInputHost",
        "ctfmon",
        "SearchIndexer",
        "SecurityHealthSystray",
        "smartscreen",
        "LockApp",
        "LogonUI",
        "WUDFHost",
        "AggregatorHost",
        "WidgetService",
        "Widgets",
        "PhoneExperienceHost",
        "GameBar",
        "GameBarFTServer",
        "SystemSettingsBroker",
        "CredentialUIBroker",
        "backgroundTaskHost"
    };

    public static int StopUserApplications(IProgress<string>? log)
    {
        var currentProcessId = Environment.ProcessId;
        var currentSessionId = Process.GetCurrentProcess().SessionId;
        var windowsDirectory = Path.GetFullPath(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var stopped = 0;
        log?.Report(LocalizationService.Get("UserProcessStop_Starting"));

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                if (process.SessionId == 0 || process.SessionId != currentSessionId)
                {
                    continue;
                }

                var processName = process.ProcessName;
                if (ProtectedProcessNames.Contains(processName))
                {
                    continue;
                }

                if (IsWindowsSystemProcess(process, windowsDirectory))
                {
                    continue;
                }

                if (!ShouldStopProcess(process))
                {
                    continue;
                }

                log?.Report(LocalizationService.Format("UserProcessStop_Terminating", processName, process.Id));
                process.Kill(entireProcessTree: true);
                stopped++;
            }
            catch (Exception ex)
            {
                log?.Report(LocalizationService.Format("UserProcessStop_SkipFailed", process.ProcessName, ex.Message));
            }
            finally
            {
                process.Dispose();
            }
        }

        log?.Report(LocalizationService.Format("UserProcessStop_Completed", stopped));
        return stopped;
    }

    private static bool ShouldStopProcess(Process process)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
            {
                return true;
            }

            var fileName = process.ProcessName;
            if (fileName.Contains("riot", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("vanguard", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("league", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("valorant", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("unkclub", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("emulator", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return process.SessionId > 0 && HasUserOwnedModule(process);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasUserOwnedModule(Process process)
    {
        try
        {
            _ = process.MainModule;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindowsSystemProcess(Process process, string windowsDirectory)
    {
        try
        {
            var modulePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(modulePath))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(modulePath);
            return fullPath.StartsWith(windowsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
