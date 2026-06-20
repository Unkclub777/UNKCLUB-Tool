using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class SystemChecks
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteFile(string lpFileName);

    public static void UnblockFile(string path)
    {
        var fullPath = ExpandPath(path);
        DeleteFile($"{fullPath}:Zone.Identifier");
    }

    public static void UnblockFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            UnblockFile(file);
        }
    }

    public static bool ShouldSkip(InstallStep step)
    {
        if (step.SkipIf is null)
        {
            return false;
        }

        return EvaluateSkipCondition(step.SkipIf);
    }

    public static bool EvaluateSkipCondition(SkipCondition condition)
    {
        if (!string.IsNullOrWhiteSpace(condition.PathExists) &&
            File.Exists(ExpandPath(condition.PathExists)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(condition.RegistryKey) &&
            RegistryMatchesWithWow64Fallback(
                condition.RegistryKey,
                condition.RegistryValueName,
                condition.RegistryExpectedValue))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(condition.Command))
        {
            var result = RunProcess(
                condition.Command,
                condition.Arguments,
                null,
                condition.SuccessExitCodes,
                60);

            return result.Success;
        }

        return false;
    }

    public static bool PathExists(string path) => File.Exists(ExpandPath(path));

    public static bool RegistryMatches(string keyPath, string? valueName, string? expectedValue)
    {
        var (hive, subKey) = ParseRegistryKey(keyPath);
        using var key = hive.OpenSubKey(subKey);
        if (key is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(valueName))
        {
            return true;
        }

        var actual = key.GetValue(valueName)?.ToString();
        if (string.IsNullOrWhiteSpace(expectedValue))
        {
            return actual is not null;
        }

        return string.Equals(actual, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    public static bool RegistryMatchesWithWow64Fallback(string keyPath, string? valueName, string? expectedValue)
    {
        if (RegistryMatches(keyPath, valueName, expectedValue))
        {
            return true;
        }

        if (!keyPath.StartsWith(@"HKLM\SOFTWARE\", StringComparison.OrdinalIgnoreCase) ||
            keyPath.Contains(@"Wow6432Node\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var wowPath = keyPath.Replace(
            @"HKLM\SOFTWARE\",
            @"HKLM\SOFTWARE\Wow6432Node\",
            StringComparison.OrdinalIgnoreCase);

        return RegistryMatches(wowPath, valueName, expectedValue);
    }

    public static bool IsDirectX9RuntimeInstalled()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (File.Exists(Path.Combine(systemDirectory, "d3dx9_43.dll")) ||
            File.Exists(Path.Combine(windowsDirectory, "SysWOW64", "d3dx9_43.dll")))
        {
            return true;
        }

        return RegistryMatchesWithWow64Fallback(@"HKLM\SOFTWARE\Microsoft\DirectX", "Version", null);
    }

    public static bool EnvironmentContains(string variableName, string expectedContains)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(expectedContains, StringComparison.OrdinalIgnoreCase);
    }

    public static ProcessRunResult RunProcess(
        string command,
        string? arguments,
        string? workingDirectory,
        IReadOnlyList<int> successExitCodes,
        int timeoutSeconds,
        bool waitForExit = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveCommandPath(command),
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : ExpandPath(workingDirectory),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
                              ?? throw new InvalidOperationException($"Komut başlatılamadı: {command}");

        if (!waitForExit)
        {
            return new ProcessRunResult(true, 0, string.Empty, string.Empty);
        }

        var completed = process.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds));
        if (!completed)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // ignored
            }

            return new ProcessRunResult(false, -1, string.Empty, "Zaman aşımına uğradı.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        var success = successExitCodes.Contains(process.ExitCode);

        return new ProcessRunResult(success, process.ExitCode, stdout, stderr);
    }

    public static bool OutputMatches(string output, string? successPattern, string? failurePattern)
    {
        if (!string.IsNullOrWhiteSpace(failurePattern) &&
            Regex.IsMatch(output, failurePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(successPattern))
        {
            return true;
        }

        return Regex.IsMatch(output, successPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string ExpandPath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (IsBareCommandName(expanded))
        {
            return ResolveCommandPath(expanded);
        }

        return DesktopPathService.NormalizeDesktopPath(path);
    }

    public static string ResolveCommandPath(string command)
    {
        var expanded = Environment.ExpandEnvironmentVariables(command).Trim();
        if (string.IsNullOrWhiteSpace(expanded))
        {
            throw new InvalidOperationException("Komut boş olamaz.");
        }

        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        if (HasPathSeparator(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        var knownPath = ResolveKnownSystemExecutable(expanded);
        if (knownPath is not null)
        {
            return knownPath;
        }

        var onPath = FindExecutableOnPath(expanded);
        if (onPath is not null)
        {
            return onPath;
        }

        var system32Candidate = Path.Combine(Environment.SystemDirectory, expanded);
        if (File.Exists(system32Candidate))
        {
            return system32Candidate;
        }

        return expanded;
    }

    private static bool IsBareCommandName(string path) =>
        !Path.IsPathRooted(path) && !HasPathSeparator(path);

    private static bool HasPathSeparator(string path) =>
        path.Contains('\\') || path.Contains('/');

    private static string? ResolveKnownSystemExecutable(string fileName)
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var knownPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["powershell.exe"] = Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["pwsh.exe"] = Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "pwsh.exe"),
            ["cmd.exe"] = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            ["msiexec.exe"] = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
            ["reg.exe"] = Path.Combine(Environment.SystemDirectory, "reg.exe"),
            ["net.exe"] = Path.Combine(Environment.SystemDirectory, "net.exe"),
            ["sc.exe"] = Path.Combine(Environment.SystemDirectory, "sc.exe")
        };

        if (knownPaths.TryGetValue(fileName, out var knownPath) && File.Exists(knownPath))
        {
            return knownPath;
        }

        return null;
    }

    private static string? FindExecutableOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var directory in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
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
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            _ => throw new InvalidOperationException($"Desteklenmeyen registry hive: {parts[0]}")
        };

        return (hive, parts[1]);
    }
}

public readonly record struct ProcessRunResult(
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public static class DownloadService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public static async Task<string> DownloadFileAsync(
        string url,
        string? fileName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PreInstallTool",
            "Downloads");

        Directory.CreateDirectory(downloadsDir);

        fileName ??= Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"download-{Guid.NewGuid():N}.tmp";
        }

        var targetPath = Path.Combine(downloadsDir, fileName);
        progress?.Report(LocalizationService.Format("Downloading", fileName));

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = DownloadProgressReporter.TryGetContentLength(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(targetPath);
        await DownloadProgressReporter.CopyWithProgressAsync(
            stream,
            fileStream,
            totalBytes,
            fileName,
            progress,
            cancellationToken);

        progress?.Report(LocalizationService.Format("DownloadComplete", targetPath));
        return targetPath;
    }
}
