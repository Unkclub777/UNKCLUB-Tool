using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class RiotVanguardService
{
    public static readonly string[] DefaultVanguardServices = ["vgk", "vgc", "vgm"];

    /// <summary>
    /// Known Vanguard/Riot processes to terminate (Task Manager order).
    /// </summary>
    public static readonly string[] DefaultVanguardTerminationProcesses =
    [
        "vgtray",
        "vgc",
        "vgk",
        "vgm",
        "RiotClientCrashHandler",
        "RiotClientServices",
        "Riot Client",
        "RiotClientUx",
        "RiotClientUxRender",
        "LeagueClient",
        "LeagueClientUx"
    ];

    public static readonly string[] DefaultVanguardProcesses = DefaultVanguardTerminationProcesses;

    private static readonly string[] VanguardProcessNamePatterns =
    [
        "vgtray",
        "vgc",
        "vgm",
        "vgk",
        "vanguard",
        "riotclient",
        "leagueclient"
    ];

    private static readonly HashSet<string> ProtectedProcessNames =
        new(StringComparer.OrdinalIgnoreCase) { "red" };

    private const int TerminationMaxAttempts = 3;
    private const int TerminationRetryDelayMs = 2000;

    public static readonly string[] DefaultRiotServices =
    [
        "RiotClientServices",
        "Riot Client"
    ];

    public static readonly string[] DefaultRiotProcesses =
    [
        "RiotClientServices",
        "RiotClientCrashHandler",
        "Riot Client",
        "RiotClientUx",
        "RiotClientUxRender",
        "LeagueClient",
        "LeagueClientUx"
    ];

    public static readonly string[] DefaultRiotClientPaths =
    [
        @"C:\Riot Games\Riot Client\RiotClientServices.exe",
        @"C:\Program Files\Riot Games\Riot Client\RiotClientServices.exe",
        @"C:\Program Files (x86)\Riot Games\Riot Client\RiotClientServices.exe"
    ];

    public static readonly string[] DefaultVanguardFolders =
    [
        @"C:\Program Files\Riot Vanguard"
    ];

    public static int StopServices(IEnumerable<string> serviceNames, IProgress<string>? log)
    {
        var stopped = 0;
        foreach (var serviceName in serviceNames.Where(static n => !string.IsNullOrWhiteSpace(n)))
        {
            if (TryStopService(serviceName, log))
            {
                stopped++;
            }
        }

        return stopped;
    }

    public static int RunScStop(IEnumerable<string> serviceNames, IProgress<string>? log)
    {
        var stopped = 0;
        foreach (var serviceName in serviceNames.Where(static n => !string.IsNullOrWhiteSpace(n)))
        {
            log?.Report(LocalizationService.Format("ErrorFix_ScStopService", serviceName));

            var result = SystemChecks.RunProcess(
                "sc.exe",
                $"stop {serviceName}",
                null,
                [0, 1060, 1062, 1056],
                60);

            var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
            if (!string.IsNullOrWhiteSpace(output))
            {
                log?.Report(output);
            }

            if (result.Success || output.Contains("STOP_PENDING", StringComparison.OrdinalIgnoreCase))
            {
                stopped++;
            }
        }

        return stopped;
    }

    public static bool TryStopService(string serviceName, IProgress<string>? log)
    {
        var status = QueryService(serviceName);
        if (status is null)
        {
            log?.Report(LocalizationService.Format("ErrorFix_ServiceNotInstalled", serviceName));
            return true;
        }

        if (status.Value.State is ServiceRunState.Stopped or ServiceRunState.NotFound)
        {
            log?.Report(LocalizationService.Format("ErrorFix_ServiceAlreadyStopped", serviceName));
            return true;
        }

        var result = SystemChecks.RunProcess(
            "sc.exe",
            $"stop {serviceName}",
            null,
            [0, 1062, 1056],
            120);

        var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            log?.Report(output);
        }

        return result.Success ||
               output.Contains("STOP_PENDING", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("1062", StringComparison.OrdinalIgnoreCase);
    }

    public static bool DeleteFolder(string folderPath, IProgress<string>? log)
    {
        var expanded = SystemChecks.ExpandPath(folderPath);
        if (!Directory.Exists(expanded))
        {
            log?.Report(LocalizationService.Format("ErrorFix_FolderNotFound", expanded));
            return true;
        }

        try
        {
            Directory.Delete(expanded, recursive: true);
            log?.Report(LocalizationService.Format("ErrorFix_FolderDeleted", expanded));
            return true;
        }
        catch (Exception ex)
        {
            log?.Report(LocalizationService.Format("ErrorFix_FolderDeleteFailed", expanded, ex.Message));
            return false;
        }
    }

    public static string? FindRiotClientExecutable(IReadOnlyList<string>? configuredPaths)
    {
        var candidates = configuredPaths is { Count: > 0 }
            ? configuredPaths
            : DefaultRiotClientPaths;

        foreach (var candidate in candidates)
        {
            var expanded = SystemChecks.ExpandPath(candidate);
            if (File.Exists(expanded))
            {
                return expanded;
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var searchRoots =
            new[] { @"C:\Riot Games", programFiles, programFilesX86 }
                .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                var match = Directory
                    .EnumerateFiles(root, "RiotClientServices.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (match is not null)
                {
                    return match;
                }
            }
            catch
            {
                // Best-effort search.
            }
        }

        return null;
    }

    public static bool LaunchProgram(string? configuredPath, IReadOnlyList<string>? searchPaths, IProgress<string>? log)
    {
        var path = !string.IsNullOrWhiteSpace(configuredPath)
            ? SystemChecks.ExpandPath(configuredPath)
            : FindRiotClientExecutable(searchPaths);

        if (path is null || !File.Exists(path))
        {
            return false;
        }

        log?.Report(LocalizationService.Format("ErrorFix_LaunchingProgram", path));

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
        });

        return true;
    }

    public static async Task<ServiceVerificationResult> WaitForServicesAsync(
        IEnumerable<string> serviceNames,
        int timeoutSeconds,
        bool setStartupAutomatic,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        var names = serviceNames.Where(static n => !string.IsNullOrWhiteSpace(n)).ToList();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var fixedStartupTypes = false;
        var lastStartAttempt = DateTime.MinValue;
        var lastLoggedStates = new Dictionary<string, ServiceRunState>(StringComparer.OrdinalIgnoreCase);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allRunning = true;
            var needsStartAttempt = false;

            foreach (var serviceName in names)
            {
                var status = QueryService(serviceName);
                if (status is null)
                {
                    log?.Report(LocalizationService.Format("ErrorFix_ServiceNotInstalled", serviceName));
                    allRunning = false;
                    continue;
                }

                if (setStartupAutomatic &&
                    status.Value.StartType is not ServiceStartType.Automatic)
                {
                    log?.Report(LocalizationService.Format("ErrorFix_SettingServiceAutomatic", serviceName));
                    SetServiceAutomatic(serviceName, log);
                    fixedStartupTypes = true;
                }

                status = QueryService(serviceName);
                if (status is not { State: ServiceRunState.Running })
                {
                    if (status?.State is ServiceRunState.Stopped or ServiceRunState.Unknown)
                    {
                        needsStartAttempt = true;
                    }

                    if (!lastLoggedStates.TryGetValue(serviceName, out var previousState) ||
                        previousState != status?.State)
                    {
                        log?.Report(LocalizationService.Format(
                            "ErrorFix_ServiceWaiting",
                            serviceName,
                            status?.State.ToString() ?? "?"));
                        lastLoggedStates[serviceName] = status?.State ?? ServiceRunState.Unknown;
                    }

                    allRunning = false;
                }
                else
                {
                    lastLoggedStates[serviceName] = ServiceRunState.Running;
                }
            }

            if (allRunning)
            {
                log?.Report(LocalizationService.Get("ErrorFix_ServicesVerified"));
                return new ServiceVerificationResult(true, fixedStartupTypes);
            }

            if (needsStartAttempt &&
                (DateTime.UtcNow - lastStartAttempt).TotalSeconds >= 5)
            {
                foreach (var serviceName in names)
                {
                    var status = QueryService(serviceName);
                    if (status is not { State: ServiceRunState.Running })
                    {
                        log?.Report(LocalizationService.Format("ErrorFix_StartingService", serviceName));
                        TryStartService(serviceName, log);
                    }
                }

                lastStartAttempt = DateTime.UtcNow;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

        return new ServiceVerificationResult(false, fixedStartupTypes);
    }

    public static VanguardStopResult StopVanguardProcesses(
        IProgress<string>? log,
        IEnumerable<string>? serviceNames = null,
        IEnumerable<string>? processNames = null)
    {
        log?.Report(LocalizationService.Get("ErrorFix_VanguardProcessStopStarting"));

        var services = (serviceNames ?? DefaultVanguardServices).Where(static n => !string.IsNullOrWhiteSpace(n)).ToList();
        var baseProcesses = (processNames ?? DefaultVanguardTerminationProcesses)
            .Where(static n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var stoppedServices = 0;
        var totalKilled = 0;
        var allTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var attempt = 1; attempt <= TerminationMaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                log?.Report(LocalizationService.Format("ErrorFix_VanguardProcessStopRetry", attempt, TerminationMaxAttempts));
            }

            stoppedServices = RunScStop(services, log);

            var targets = CollectTargetProcessNames(baseProcesses, log, attempt == 1);
            foreach (var target in targets)
            {
                allTargets.Add(target);
            }

            totalKilled += ForceKillProcesses(targets, log);

            if (!HasRunningTargets(baseProcesses))
            {
                break;
            }

            if (attempt < TerminationMaxAttempts)
            {
                Thread.Sleep(TerminationRetryDelayMs);
            }
        }

        log?.Report(LocalizationService.Format(
            "ErrorFix_VanguardProcessStopSummary",
            stoppedServices,
            totalKilled));

        return new VanguardStopResult(stoppedServices, totalKilled, allTargets.Count);
    }

    public static VanguardStopResult StopVanguardCompletely(IProgress<string>? log) =>
        StopVanguardProcesses(log);

    public static bool IncludesVanguardServices(IEnumerable<string> serviceNames) =>
        serviceNames.Any(name =>
            name.Equals("vgk", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("vgc", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("vgm", StringComparison.OrdinalIgnoreCase));

    public static bool SetServiceAutomatic(string serviceName, IProgress<string>? log)
    {
        var result = SystemChecks.RunProcess(
            "sc.exe",
            $"config {serviceName} start= auto",
            null,
            [0, 1072],
            60);

        var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            log?.Report(output);
        }

        return result.Success;
    }

    public static bool TryStartService(string serviceName, IProgress<string>? log)
    {
        var result = SystemChecks.RunProcess(
            "sc.exe",
            $"start {serviceName}",
            null,
            [0, 1056, 1058],
            120);

        var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            log?.Report(output);
        }

        return result.Success ||
               output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    public static int StopProcesses(IEnumerable<string> processNames, IProgress<string>? log)
    {
        var stopped = 0;
        foreach (var processName in processNames.Where(static n => !string.IsNullOrWhiteSpace(n)))
        {
            stopped += StopProcessByName(processName, log);
        }

        return stopped;
    }

    public static int ForceKillProcesses(IEnumerable<string> processNames, IProgress<string>? log)
    {
        var killed = 0;
        foreach (var processName in processNames.Where(static n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            killed += ForceKillProcessByName(processName, log);
        }

        return killed;
    }

    public static List<string> DiscoverVanguardRelatedProcessNames()
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                if (IsProtectedProcess(name))
                {
                    continue;
                }

                if (IsVanguardRelatedProcessName(name) || IsRiotRelatedProcessName(name))
                {
                    discovered.Add(name);
                    continue;
                }

                if (TryGetProcessPath(process, out var path) &&
                    (IsVanguardRelatedPath(path) || IsRiotRelatedPath(path)))
                {
                    discovered.Add(name);
                }
            }
            catch
            {
                // Access denied or process exited.
            }
            finally
            {
                process.Dispose();
            }
        }

        return discovered.ToList();
    }

    private static List<string> CollectTargetProcessNames(
        IReadOnlyList<string> baseProcesses,
        IProgress<string>? log,
        bool logDiscovery)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in baseProcesses)
        {
            targets.Add(name);
        }

        if (logDiscovery)
        {
            log?.Report(LocalizationService.Get("ErrorFix_VanguardProcessDiscoveryStarting"));
        }

        foreach (var name in DiscoverVanguardRelatedProcessNames())
        {
            if (targets.Add(name) && logDiscovery)
            {
                log?.Report(LocalizationService.Format("ErrorFix_VanguardProcessDiscovered", name));
            }
        }

        return targets.ToList();
    }

    private static bool HasRunningTargets(IEnumerable<string> processNames)
    {
        foreach (var processName in processNames.Where(static n => !string.IsNullOrWhiteSpace(n)))
        {
            if (IsProcessRunning(processName))
            {
                return true;
            }
        }

        foreach (var name in DiscoverVanguardRelatedProcessNames())
        {
            if (IsProcessRunning(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProcessRunning(string processName)
    {
        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var processes = Process.GetProcessesByName(normalized);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsVanguardRelatedProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var pattern in VanguardProcessNamePatterns)
        {
            if (processName.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                processName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return processName.Contains("Vanguard", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRiotRelatedProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return processName.StartsWith("RiotClient", StringComparison.OrdinalIgnoreCase) ||
               processName.StartsWith("LeagueClient", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("Riot Client", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtectedProcess(string processName)
    {
        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        return ProtectedProcessNames.Contains(normalized);
    }

    private static bool IsRiotRelatedPath(string path) =>
        path.Contains(@"Riot Games", StringComparison.OrdinalIgnoreCase);

    private static bool IsVanguardRelatedPath(string path) =>
        path.Contains(@"Riot Vanguard", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(@"\vgk.", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(@"\vgc.", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(@"\vgm.", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(@"\vgtray.", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetProcessPath(Process process, out string path)
    {
        path = string.Empty;
        try
        {
            path = process.MainModule?.FileName ?? string.Empty;
            return !string.IsNullOrWhiteSpace(path);
        }
        catch
        {
            return false;
        }
    }

    private static int ForceKillProcessByName(string processName, IProgress<string>? log)
    {
        if (IsProtectedProcess(processName))
        {
            return 0;
        }

        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";

        var running = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(normalized));
        var runningCount = running.Length;
        foreach (var process in running)
        {
            process.Dispose();
        }

        if (runningCount == 0)
        {
            return 0;
        }

        log?.Report(LocalizationService.Format("ErrorFix_ForceKillingProcess", normalized));

        var result = SystemChecks.RunProcess(
            "taskkill.exe",
            $"/F /IM {normalized}",
            null,
            [0, 128, 255],
            30);

        var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            log?.Report(output);
        }

        if (result.Success ||
            output.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return runningCount;
        }

        if (output.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("128", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return result.ExitCode == 0 ? runningCount : 0;
    }

    private static int StopProcessByName(string processName, IProgress<string>? log)
    {
        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var processes = Process.GetProcessesByName(normalized);
        if (processes.Length == 0)
        {
            return 0;
        }

        var stopped = 0;
        foreach (var process in processes)
        {
            try
            {
                log?.Report(LocalizationService.Format("ErrorFix_StoppingProcess", process.ProcessName));

                if (!process.HasExited && process.CloseMainWindow())
                {
                    if (process.WaitForExit(5000))
                    {
                        stopped++;
                        continue;
                    }
                }

                if (!process.HasExited)
                {
                    process.Kill(true);
                    process.WaitForExit(5000);
                }

                stopped++;
            }
            catch (Exception ex)
            {
                log?.Report(LocalizationService.Format("ErrorFix_ProcessStopFailed", process.ProcessName, ex.Message));
            }
            finally
            {
                process.Dispose();
            }
        }

        return stopped;
    }

    public static ServiceStatus? QueryService(string serviceName)
    {
        var queryResult = SystemChecks.RunProcess(
            "sc.exe",
            $"query {serviceName}",
            null,
            [0, 1060],
            30);

        var queryOutput = $"{queryResult.StandardOutput}\n{queryResult.StandardError}";
        if (queryOutput.Contains("1060", StringComparison.OrdinalIgnoreCase) ||
            queryOutput.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var state = ParseRunState(queryOutput);
        var configResult = SystemChecks.RunProcess(
            "sc.exe",
            $"qc {serviceName}",
            null,
            [0, 1060],
            30);

        var configOutput = $"{configResult.StandardOutput}\n{configResult.StandardError}";
        var startType = ParseStartType(configOutput);

        return new ServiceStatus(serviceName, state, startType);
    }

    private static ServiceRunState ParseRunState(string output)
    {
        if (Regex.IsMatch(output, @"STATE\s+:\s+\d+\s+STOPPED", RegexOptions.IgnoreCase))
        {
            return ServiceRunState.Stopped;
        }

        if (Regex.IsMatch(output, @"STATE\s+:\s+\d+\s+RUNNING", RegexOptions.IgnoreCase))
        {
            return ServiceRunState.Running;
        }

        if (Regex.IsMatch(output, @"STATE\s+:\s+\d+\s+START_PENDING", RegexOptions.IgnoreCase))
        {
            return ServiceRunState.StartPending;
        }

        if (Regex.IsMatch(output, @"STATE\s+:\s+\d+\s+STOP_PENDING", RegexOptions.IgnoreCase))
        {
            return ServiceRunState.StopPending;
        }

        return ServiceRunState.Unknown;
    }

    private static ServiceStartType ParseStartType(string output)
    {
        if (Regex.IsMatch(output, @"START_TYPE\s+:\s+\d+\s+DISABLED", RegexOptions.IgnoreCase))
        {
            return ServiceStartType.Disabled;
        }

        if (Regex.IsMatch(output, @"START_TYPE\s+:\s+\d+\s+AUTO_START", RegexOptions.IgnoreCase))
        {
            return ServiceStartType.Automatic;
        }

        if (Regex.IsMatch(output, @"START_TYPE\s+:\s+\d+\s+DEMAND_START", RegexOptions.IgnoreCase))
        {
            return ServiceStartType.Manual;
        }

        return ServiceStartType.Unknown;
    }
}

public enum ServiceRunState
{
    Unknown,
    NotFound,
    Stopped,
    Running,
    StartPending,
    StopPending
}

public enum ServiceStartType
{
    Unknown,
    Automatic,
    Manual,
    Disabled
}

public readonly record struct ServiceStatus(
    string Name,
    ServiceRunState State,
    ServiceStartType StartType);

public readonly record struct ServiceVerificationResult(
    bool AllRunning,
    bool FixedStartupTypes);

public readonly record struct VanguardStopResult(
    int StoppedServices,
    int KilledProcesses,
    int DiscoveredProcesses);
