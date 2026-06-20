using System.IO;
using System.Text;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

/// <summary>
/// Adds Windows Defender exclusions before Defender is disabled, to reduce false-positive
/// detections on PreInstallTool and UNKCLUB during install.
/// </summary>
public static class DefenderExclusionService
{
    public static readonly string[] ExclusionProcesses = ["PreInstallTool.exe", "UNKCLUB.exe"];

    public static IReadOnlyList<string> GetExclusionPaths()
    {
        var paths = new List<string>
        {
            Path.GetFullPath(AppContext.BaseDirectory),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Installers")),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Emulator"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PreInstallTool")
        };

        return paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static InstallResult AddExclusions(IProgress<string>? log = null)
    {
        log?.Report(LocalizationService.Get("DefenderExclusionsAdding"));

        var paths = GetExclusionPaths();
        var script = BuildExclusionScript(paths);
        var result = RunPowerShell(script, 120);

        AddRegistryExclusions(paths);

        if (result.Success)
        {
            log?.Report(LocalizationService.Get("DefenderExclusionsAdded"));
            return new InstallResult(true, LocalizationService.Get("DefenderExclusionsAdded"));
        }

        log?.Report(LocalizationService.Format("DefenderExclusionsWarning", result.ExitCode));
        return new InstallResult(true, LocalizationService.Get("DefenderExclusionsPartial"));
    }

    private static void AddRegistryExclusions(IReadOnlyList<string> paths)
    {
        try
        {
            using var pathsKey = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows Defender\Exclusions\Paths",
                writable: true);

            if (pathsKey is not null)
            {
                foreach (var path in paths)
                {
                    pathsKey.SetValue(path, 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }

            using var processesKey = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows Defender\Exclusions\Processes",
                writable: true);

            if (processesKey is not null)
            {
                foreach (var process in ExclusionProcesses)
                {
                    processesKey.SetValue(process, 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
        }
        catch
        {
            // Registry fallback is best-effort; Set-MpPreference is primary.
        }
    }

    private static string BuildExclusionScript(IReadOnlyList<string> paths)
    {
        var pathLiterals = string.Join(
            ", ",
            paths.Select(static path => $"'{path.Replace("'", "''")}'"));

        var processLiterals = string.Join(
            ", ",
            ExclusionProcesses.Select(static process => $"'{process}'"));

        return
            "$ErrorActionPreference = 'SilentlyContinue'\r\n\r\n" +
            "if (Get-Command Add-MpPreference -ErrorAction SilentlyContinue) {\r\n" +
            $"    Add-MpPreference -ExclusionPath @({pathLiterals}) -ErrorAction SilentlyContinue\r\n" +
            $"    Add-MpPreference -ExclusionProcess @({processLiterals}) -ErrorAction SilentlyContinue\r\n" +
            "}\r\n\r\n" +
            "exit 0\r\n";
    }

    private static (bool Success, int ExitCode) RunPowerShell(string script, int timeoutSeconds)
    {
        var arguments = new StringBuilder()
            .Append("-NoProfile -ExecutionPolicy Bypass -Command \"")
            .Append(script.Replace("\"", "`\""))
            .Append('"')
            .ToString();

        var result = SystemChecks.RunProcess(
            "powershell.exe",
            arguments,
            null,
            [0],
            timeoutSeconds,
            waitForExit: true);

        return (result.Success, result.ExitCode);
    }
}
