using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public sealed class InstallOrchestrator
{
    public Task<IReadOnlyList<StepProgress>> RunAsync(
        InstallConfig config,
        IProgress<(int current, int total, StepProgress step)>? progress,
        IProgress<string>? log,
        CancellationToken cancellationToken,
        Action? defenderStatusRefresh = null) =>
        RunAsync(config.Steps, progress, log, cancellationToken, defenderStatusRefresh);

    public async Task<IReadOnlyList<StepProgress>> RunAsync(
        IReadOnlyList<InstallStep> steps,
        IProgress<(int current, int total, StepProgress step)>? progress,
        IProgress<string>? log,
        CancellationToken cancellationToken,
        Action? defenderStatusRefresh = null)
    {
        var enabledSteps = steps.Where(static step => step.Enabled).ToList();
        var results = enabledSteps
            .Select(static step => new StepProgress { Step = step })
            .ToList();

        for (var index = 0; index < results.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = results[index];
            item.Status = StepStatus.Running;
            item.Message = LocalizationService.Get("StepRunning");
            progress?.Report((index + 1, results.Count, item));
            log?.Report(LocalizationService.Format("StepStarted", LocalizedStepName(item.Step)));

            try
            {
                if (SystemChecks.ShouldSkip(item.Step))
                {
                    item.Status = StepStatus.Skipped;
                    item.Message = LocalizationService.Get("StepSkippedAlreadyInstalled");
                    log?.Report(LocalizationService.Format("StepSkipped", LocalizedStepName(item.Step)));
                    progress?.Report((index + 1, results.Count, item));
                    continue;
                }

                var result = await ExecuteStepAsync(item.Step, log, cancellationToken, defenderStatusRefresh);
                if (result.Skipped)
                {
                    item.Status = StepStatus.Skipped;
                    item.Message = result.Message;
                }
                else if (result.Success)
                {
                    item.Status = StepStatus.Success;
                    item.Message = result.Message;
                }
                else
                {
                    item.Status = StepStatus.Failed;
                    item.Message = result.Message;
                    log?.Report(LocalizationService.Format("StepFailed", LocalizedStepName(item.Step), result.Message));

                    if (!item.Step.Optional)
                    {
                        break;
                    }
                }

                log?.Report(LocalizationService.Format("StepResultLog", LocalizedStepName(item.Step), item.Message));
            }
            catch (Exception ex)
            {
                item.Status = StepStatus.Failed;
                item.Message = ex.Message;
                log?.Report(LocalizationService.Format("StepError", LocalizedStepName(item.Step), ex.Message));

                if (!item.Step.Optional)
                {
                    break;
                }
            }

            progress?.Report((index + 1, results.Count, item));
        }

        return results;
    }

    public void LaunchMainProgram(InstallConfig config)
    {
        if (!config.LaunchMainProgramWhenDone ||
            string.IsNullOrWhiteSpace(config.MainProgramPath))
        {
            return;
        }

        var path = SystemChecks.ExpandPath(config.MainProgramPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(LocalizationService.Get("MainProgramNotFound"), path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = config.MainProgramArgs ?? string.Empty,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
        });
    }

    private static async Task<InstallResult> ExecuteStepAsync(
        InstallStep step,
        IProgress<string>? log,
        CancellationToken cancellationToken,
        Action? defenderStatusRefresh = null)
    {
        return step.Type.ToLowerInvariant() switch
        {
            "checkcommand" => ExecuteCheckCommand(step),
            "checkpath" => ExecuteCheckPath(step),
            "checkregistry" => ExecuteCheckRegistry(step),
            "checkenvironment" => ExecuteCheckEnvironment(step),
            "runcommand" => ExecuteRunCommand(step, log),
            "disabledefender" => ExecuteDisableDefender(step, log, defenderStatusRefresh),
            "download" => await ExecuteDownloadAsync(step, log, cancellationToken),
            "downloadandrun" => await ExecuteDownloadAndRunAsync(step, log, cancellationToken),
            "installfolder" => ExecuteInstallFolder(step, log),
            "deployfile" => ExecuteDeployFile(step, log),
            "message" => CreateMessageStepResult(step),
            "stopservices" => ExecuteStopServices(step, log),
            "runscstop" => ExecuteRunScStop(step, log),
            "deletefolder" => ExecuteDeleteFolder(step, log),
            "launchprogram" => ExecuteLaunchProgram(step, log),
            "waitforservices" => await ExecuteWaitForServicesAsync(step, log, cancellationToken),
            "stopvanguard" => ExecuteStopVanguardProcesses(step, log),
            "stopvanguardprocesses" => ExecuteStopVanguardProcesses(step, log),
            "stopuserprocesses" => ExecuteStopUserProcesses(step, log),
            "ensureservicesautomatic" => ExecuteEnsureServicesAutomatic(step, log),
            "restartcomputer" => ExecuteRestartComputer(step, log),
            "rundesktopapp" => ExecuteRunDesktopApp(step, log),
            "checkvanguard" => ExecuteCheckVanguard(step, log),
            _ => new InstallResult(false, LocalizationService.Format("UnknownStepType", step.Type))
        };
    }

    private static InstallResult ExecuteCheckCommand(InstallStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Command))
        {
            return new InstallResult(false, LocalizationService.Get("CommandNotDefined"));
        }

        var result = SystemChecks.RunProcess(
            step.Command,
            step.Arguments,
            step.WorkingDirectory,
            step.SuccessExitCodes,
            step.TimeoutSeconds,
            step.WaitForExit);

        var output = $"{result.StandardOutput}\n{result.StandardError}";
        var matches = SystemChecks.OutputMatches(output, step.SuccessPattern, step.FailurePattern);

        if (result.Success && matches)
        {
            return new InstallResult(true, LocalizationService.Get("CheckSuccess"));
        }

        return new InstallResult(false, LocalizationService.Format("CheckFailed", result.ExitCode));
    }

    private static InstallResult ExecuteCheckPath(InstallStep step)
    {
        if (string.IsNullOrWhiteSpace(step.PathExists))
        {
            return new InstallResult(false, LocalizationService.Get("PathExistsNotDefined"));
        }

        var exists = SystemChecks.PathExists(step.PathExists);
        return exists
            ? new InstallResult(true, LocalizationService.Get("PathExists"))
            : new InstallResult(false, LocalizationService.Format("PathNotFound", step.PathExists));
    }

    private static InstallResult ExecuteCheckRegistry(InstallStep step)
    {
        if (string.IsNullOrWhiteSpace(step.RegistryKey))
        {
            return new InstallResult(false, LocalizationService.Get("RegistryKeyNotDefined"));
        }

        var matches = SystemChecks.RegistryMatches(
            step.RegistryKey,
            step.RegistryValueName,
            step.RegistryExpectedValue);

        return matches
            ? new InstallResult(true, LocalizationService.Get("RegistryCheckSuccess"))
            : new InstallResult(false, LocalizationService.Get("RegistryNotFound"));
    }

    private static InstallResult ExecuteCheckEnvironment(InstallStep step)
    {
        if (string.IsNullOrWhiteSpace(step.EnvironmentVariable) ||
            string.IsNullOrWhiteSpace(step.EnvironmentExpectedContains))
        {
            return new InstallResult(false, LocalizationService.Get("EnvVarMissing"));
        }

        var matches = SystemChecks.EnvironmentContains(
            step.EnvironmentVariable,
            step.EnvironmentExpectedContains);

        return matches
            ? new InstallResult(true, LocalizationService.Get("EnvCheckSuccess"))
            : new InstallResult(false, LocalizationService.Format("EnvCheckFailed", step.EnvironmentVariable));
    }

    private static InstallResult ExecuteDisableDefender(
        InstallStep step,
        IProgress<string>? log,
        Action? defenderStatusRefresh = null) =>
        DefenderDisableService.Disable(log, defenderStatusRefresh);

    private static InstallResult ExecuteRunCommand(InstallStep step, IProgress<string>? log)
    {
        if (string.IsNullOrWhiteSpace(step.Command))
        {
            return new InstallResult(false, LocalizationService.Get("CommandNotDefined"));
        }

        log?.Report(LocalizationService.Format("RunningCommand", step.Command, step.Arguments ?? string.Empty).Trim());

        if (step.RunAsAdmin)
        {
            return RunElevatedCommand(step);
        }

        var result = SystemChecks.RunProcess(
            step.Command,
            step.Arguments,
            step.WorkingDirectory,
            step.SuccessExitCodes,
            step.TimeoutSeconds,
            step.WaitForExit);

        return result.Success
            ? new InstallResult(true, LocalizationService.Get("CommandCompleted"))
            : new InstallResult(false, LocalizationService.Format("CommandFailed", result.ExitCode));
    }

    private static InstallResult RunElevatedCommand(InstallStep step)
    {
        var fileName = SystemChecks.ResolveCommandPath(step.Command!);
        SystemChecks.UnblockFile(fileName);

        var workingDirectory = string.IsNullOrWhiteSpace(step.WorkingDirectory)
            ? Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory
            : SystemChecks.ExpandPath(step.WorkingDirectory);

        if (IsRunningAsAdministrator())
        {
            var result = SystemChecks.RunProcess(
                fileName,
                step.Arguments,
                workingDirectory,
                step.SuccessExitCodes,
                step.TimeoutSeconds,
                step.WaitForExit);

            return result.Success
                ? new InstallResult(true, LocalizationService.Get("InstallCompleted"))
                : new InstallResult(false, LocalizationService.Format("InstallFailed", result.ExitCode));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = step.Arguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            Verb = "runas"
        };

        var process = Process.Start(startInfo);

        if (process is null)
        {
            return new InstallResult(false, LocalizationService.Get("AdminCommandFailed"));
        }

        if (step.WaitForExit)
        {
            var completed = process.WaitForExit(TimeSpan.FromSeconds(step.TimeoutSeconds));
            if (!completed)
            {
                return new InstallResult(false, LocalizationService.Get("CommandTimeout"));
            }

            return step.SuccessExitCodes.Contains(process.ExitCode)
                ? new InstallResult(true, LocalizationService.Get("AdminCommandCompleted"))
                : new InstallResult(false, LocalizationService.Format("CommandFailed", process.ExitCode));
        }

        return new InstallResult(true, LocalizationService.Get("CommandStarted"));
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<InstallResult> ExecuteDownloadAsync(
        InstallStep step,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.Url))
        {
            return new InstallResult(false, LocalizationService.Get("UrlNotDefined"));
        }

        var path = await DownloadService.DownloadFileAsync(
            step.Url,
            step.DownloadFileName,
            log,
            cancellationToken);

        return new InstallResult(true, LocalizationService.Format("Downloaded", path), downloadedPath: path);
    }

    private static InstallResult ExecuteInstallFolder(InstallStep step, IProgress<string>? log)
    {
        if (string.IsNullOrWhiteSpace(step.FolderPath))
        {
            return new InstallResult(false, LocalizationService.Get("FolderPathNotDefined"));
        }

        var folder = ResolveFolderPath(step.FolderPath);
        if (!Directory.Exists(folder))
        {
            return new InstallResult(false, LocalizationService.Format("FolderNotFound", folder));
        }

        SystemChecks.UnblockFolder(folder);
        log?.Report(LocalizationService.Get("SecurityUnblockDone"));

        var extensions = step.FileExtensions
            .Where(static ext => !string.IsNullOrWhiteSpace(ext))
            .Select(static ext => ext.StartsWith('.') ? ext : $".{ext}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (extensions.Count == 0)
        {
            extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe", ".msi", ".bat", ".cmd"
            };
        }

        var searchOption = step.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory
            .EnumerateFiles(folder, "*.*", searchOption)
            .Where(file => extensions.Contains(Path.GetExtension(file)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            return new InstallResult(false, LocalizationService.Format("NoInstallersInFolder", folder));
        }

        log?.Report(LocalizationService.Format("InstallersFound", files.Count));

        var installedCount = 0;
        var skippedCount = 0;

        for (var index = 0; index < files.Count; index++)
        {
            var filePath = files[index];
            var fileName = Path.GetFileName(filePath);
            log?.Report(LocalizationService.Format("Installing", index + 1, files.Count, fileName));

            var result = InstallerRunner.Run(filePath, step, log);

            if (result.Skipped)
            {
                skippedCount++;
                log?.Report(LocalizationService.Format("SkippedInstaller", index + 1, files.Count, fileName, result.Message));
                continue;
            }

            if (!result.Success)
            {
                return new InstallResult(false, LocalizationService.Format("InstallerFailed", fileName, result.Message));
            }

            installedCount++;
            log?.Report(LocalizationService.Format("InstallerCompleted", index + 1, files.Count, fileName));
        }

        log?.Report(LocalizationService.Format(
            "InstallFolderSummary",
            files.Count,
            installedCount,
            skippedCount));

        return new InstallResult(true, LocalizationService.Format("AllInstallersSuccess", files.Count));
    }

    private static InstallResult ExecuteDeployFile(InstallStep step, IProgress<string>? log)
    {
        if (UnkclubAppService.IsGitHubDeployStep(step))
        {
            return ExecuteDeployUnkclubFromGitHub(step, log);
        }

        if (string.IsNullOrWhiteSpace(step.SourcePath))
        {
            return new InstallResult(false, LocalizationService.Get("SourcePathNotDefined"));
        }

        var sourcePath = ResolveDeploySourcePath(step.SourcePath);
        if (sourcePath is null)
        {
            return new InstallResult(false, LocalizationService.Format("SourceFileNotFound", step.SourcePath));
        }

        string destinationPath;
        if (!string.IsNullOrWhiteSpace(step.DestinationFolder) &&
            !string.IsNullOrWhiteSpace(step.FileName))
        {
            var destinationFolder = DesktopPathService.ResolveUniqueDestinationFolder(
                SystemChecks.ExpandPath(step.DestinationFolder),
                step.DuplicateFolderStrategy);

            Directory.CreateDirectory(destinationFolder);
            destinationPath = Path.Combine(destinationFolder, NormalizeExecutableFileName(step.FileName));
        }
        else if (!string.IsNullOrWhiteSpace(step.DestinationPath))
        {
            destinationPath = NormalizeExecutableFileName(SystemChecks.ExpandPath(step.DestinationPath));
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }
        }
        else
        {
            return new InstallResult(false, LocalizationService.Get("DestinationNotDefined"));
        }

        log?.Report(LocalizationService.Format("CopyingFile", sourcePath, destinationPath));
        File.Copy(sourcePath, destinationPath, overwrite: true);
        SystemChecks.UnblockFile(destinationPath);

        return new InstallResult(true, LocalizationService.Format("FileCopied", destinationPath));
    }

    private static InstallResult ExecuteDeployUnkclubFromGitHub(InstallStep step, IProgress<string>? log)
    {
        try
        {
            var destinationPath = UnkclubAppService.ResolveDestinationPath(step);
            var deployedPath = UnkclubAppService
                .DownloadToPathAsync(destinationPath, log, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            return new InstallResult(true, LocalizationService.Format("UnkclubApp_Deployed", deployedPath));
        }
        catch (Exception ex)
        {
            return new InstallResult(false, LocalizationService.Format("UnkclubApp_DeployFailed", ex.Message));
        }
    }

    private static string? ResolveDeploySourcePath(string sourcePath)
    {
        var resolved = ResolveFolderPath(sourcePath);
        if (File.Exists(resolved))
        {
            return resolved;
        }

        var directory = Path.GetDirectoryName(resolved);
        var fileName = Path.GetFileName(resolved);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        if (fileName.Equals("UNKCLUB.exe", StringComparison.OrdinalIgnoreCase))
        {
            var doubleExtension = Path.Combine(directory, "UNKCLUB.exe.exe");
            if (File.Exists(doubleExtension))
            {
                return doubleExtension;
            }
        }

        return null;
    }

    private static string NormalizeExecutableFileName(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(".exe.exe", StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(path);
            var normalizedName = fileName[..^4];
            return string.IsNullOrWhiteSpace(directory)
                ? normalizedName
                : Path.Combine(directory, normalizedName);
        }

        return path;
    }

    private static InstallStep BuildInstallerRunStep(InstallStep parentStep, string filePath)
    {
        var extension = Path.GetExtension(filePath);
        var arguments = ResolveInstallerArguments(parentStep, filePath);

        if (extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallStep
            {
                Command = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                Arguments = $"/i \"{filePath}\" {arguments}".Trim(),
                WorkingDirectory = Path.GetDirectoryName(filePath),
                SuccessExitCodes = parentStep.SuccessExitCodes,
                RunAsAdmin = true,
                WaitForExit = parentStep.WaitForExit,
                TimeoutSeconds = parentStep.TimeoutSeconds
            };
        }

        if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallStep
            {
                Command = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = $"/c \"\"{filePath}\" {arguments}\"".Trim(),
                WorkingDirectory = Path.GetDirectoryName(filePath),
                SuccessExitCodes = parentStep.SuccessExitCodes,
                RunAsAdmin = true,
                WaitForExit = parentStep.WaitForExit,
                TimeoutSeconds = parentStep.TimeoutSeconds
            };
        }

        return new InstallStep
        {
            Command = filePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(filePath),
            SuccessExitCodes = parentStep.SuccessExitCodes,
            RunAsAdmin = true,
            WaitForExit = parentStep.WaitForExit,
            TimeoutSeconds = parentStep.TimeoutSeconds
        };
    }

    private static string ResolveInstallerArguments(InstallStep step, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(step.InstallerArguments))
        {
            return step.InstallerArguments;
        }

        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains("vcredist2005") || fileName.Contains("vcredist2008"))
        {
            return "/Q";
        }

        if (fileName.StartsWith("dxwebsetup"))
        {
            return string.Empty;
        }

        if (fileName.Contains("vcredist_v14") || fileName.Contains("vc_redist"))
        {
            return "/install /quiet /norestart";
        }

        if (fileName.Contains("vcredist"))
        {
            return "/quiet /norestart";
        }

        return string.Empty;
    }

    private static string ResolveFolderPath(string folderPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(folderPath);
        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        AppResourceService.EnsureInitialized();
        return Path.GetFullPath(Path.Combine(AppResourceService.ResourceRoot, expanded));
    }

    private static InstallResult ExecuteCheckVanguard(InstallStep step, IProgress<string>? log)
    {
        var detection = VanguardDetectionService.Detect();
        var summary = VanguardDetectionService.BuildStatusSummary(detection);
        log?.Report(summary);

        if (detection.State == VanguardInstallState.Installed)
        {
            log?.Report(LocalizationService.Get("Vanguard_DetectedOk"));
            return new InstallResult(true, LocalizationService.Get("Vanguard_DetectedOk"));
        }

        var title = string.IsNullOrWhiteSpace(step.MessageTitleKey)
            ? LocalizationService.GetString("Vanguard_WarningTitle")
            : LocalizationService.GetString(step.MessageTitleKey);

        var messageKey = detection.State == VanguardInstallState.NotInstalled
            ? "Vanguard_NotInstalledMessage"
            : "Vanguard_PartialInstallMessage";

        if (!string.IsNullOrWhiteSpace(step.MessageKey))
        {
            messageKey = step.MessageKey;
        }

        MessageBox.Show(
            LocalizationService.GetString(messageKey),
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        log?.Report(LocalizationService.Get("Vanguard_WarningShownContinuing"));

        return step.Optional
            ? new InstallResult(true, LocalizationService.Get("Vanguard_WarningShownContinuing"), skipped: false)
            : new InstallResult(false, LocalizationService.Get("Vanguard_NotReady"));
    }

    private static async Task<InstallResult> ExecuteDownloadAndRunAsync(
        InstallStep step,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        var downloadResult = await ExecuteDownloadAsync(step, log, cancellationToken);
        if (!downloadResult.Success)
        {
            return downloadResult;
        }

        var downloadedPath = downloadResult.DownloadedPath
            ?? downloadResult.Message;
        step.Command = downloadedPath;
        step.RunAsAdmin = true;

        return ExecuteRunCommand(step, log);
    }

    private static InstallResult CreateMessageStepResult(InstallStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.MessageKey))
        {
            ShowLocalizedMessage(step);
        }

        var description = LocalizationService.GetLocalizedStepDescription(step.Id, step.Description);
        var message = string.IsNullOrWhiteSpace(description)
            ? LocalizedStepName(step)
            : description;
        return new InstallResult(true, message);
    }

    private static string LocalizedStepName(InstallStep step) =>
        LocalizationService.GetLocalizedStepName(step.Id, step.Name);

    private static InstallResult ExecuteStopServices(InstallStep step, IProgress<string>? log)
    {
        var services = ResolveServiceNames(step);

        if (RiotVanguardService.IncludesVanguardServices(services))
        {
            log?.Report(LocalizationService.Get("ErrorFix_VanguardShutdownStarting"));
            var vanguardResult = RiotVanguardService.StopVanguardProcesses(log);
            log?.Report(LocalizationService.Format(
                "ErrorFix_VanguardShutdownSummary",
                vanguardResult.StoppedServices,
                vanguardResult.KilledProcesses));
        }

        if (step.StopProcesses)
        {
            var processes = ResolveProcessNames(step);
            var stoppedProcesses = RiotVanguardService.ForceKillProcesses(processes, log);
            log?.Report(LocalizationService.Format("ErrorFix_ProcessesStopped", stoppedProcesses));
        }

        var riotServices = services
            .Where(name =>
                !name.Equals("vgk", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("vgc", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var stoppedServices = riotServices.Count > 0
            ? RiotVanguardService.StopServices(riotServices, log)
            : 0;

        return new InstallResult(
            true,
            LocalizationService.Format("ErrorFix_ServicesStopped", stoppedServices, riotServices.Count));
    }

    private static InstallResult ExecuteStopVanguardProcesses(InstallStep step, IProgress<string>? log)
    {
        var services = ResolveServiceNames(step);
        if (services.Count == 0)
        {
            services = RiotVanguardService.DefaultVanguardServices.ToList();
        }

        var result = RiotVanguardService.StopVanguardProcesses(log, services);

        if (step.StopProcesses)
        {
            var processes = step.ProcessNames.Count > 0
                ? step.ProcessNames
                : RiotVanguardService.DefaultRiotProcesses.ToList();
            var stoppedProcesses = RiotVanguardService.ForceKillProcesses(processes, log);
            log?.Report(LocalizationService.Format("ErrorFix_ProcessesStopped", stoppedProcesses));
        }

        return new InstallResult(
            true,
            LocalizationService.Format(
                "ErrorFix_VanguardStopCompleted",
                result.StoppedServices,
                result.KilledProcesses));
    }

    private static InstallResult ExecuteRunScStop(InstallStep step, IProgress<string>? log)
    {
        var services = ResolveServiceNames(step);
        if (services.Count == 0)
        {
            services = RiotVanguardService.DefaultVanguardServices.ToList();
        }

        var stopped = RiotVanguardService.RunScStop(services, log);
        return new InstallResult(
            true,
            LocalizationService.Format("ErrorFix_ScStopCompleted", stopped, services.Count));
    }

    private static InstallResult ExecuteDeleteFolder(InstallStep step, IProgress<string>? log)
    {
        var folders = step.FolderPaths.Count > 0
            ? step.FolderPaths
            : RiotVanguardService.DefaultVanguardFolders.ToList();

        var deleted = 0;
        foreach (var folder in folders)
        {
            if (RiotVanguardService.DeleteFolder(folder, log))
            {
                deleted++;
            }
        }

        return deleted == folders.Count
            ? new InstallResult(true, LocalizationService.Format("ErrorFix_FoldersDeleted", deleted))
            : new InstallResult(false, LocalizationService.Get("ErrorFix_FolderDeletePartial"));
    }

    private static InstallResult ExecuteLaunchProgram(InstallStep step, IProgress<string>? log)
    {
        if (!RiotVanguardService.LaunchProgram(step.ProgramPath, step.ProgramPaths, log))
        {
            return new InstallResult(false, LocalizationService.Get("ErrorFix_RiotClientNotFound"));
        }

        ShowLocalizedMessage(step);

        return new InstallResult(true, LocalizationService.Get("ErrorFix_RiotClientLaunched"));
    }

    private static async Task<InstallResult> ExecuteWaitForServicesAsync(
        InstallStep step,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        var services = ResolveServiceNames(step);
        if (services.Count == 0)
        {
            services = RiotVanguardService.DefaultVanguardServices.ToList();
        }

        var timeout = step.WaitTimeoutSeconds > 0 ? step.WaitTimeoutSeconds : 180;
        var result = await RiotVanguardService.WaitForServicesAsync(
            services,
            timeout,
            step.SetStartupAutomatic,
            log,
            cancellationToken);

        if (!result.AllRunning)
        {
            log?.Report(LocalizationService.Get("ErrorFix_ServicesNotReadyContinuing"));
            return new InstallResult(true, LocalizationService.Get("ErrorFix_ServicesNotReadyWarning"));
        }

        if (result.FixedStartupTypes)
        {
            log?.Report(LocalizationService.Get("ErrorFix_ServicesFixed"));
            log?.Report(LocalizationService.Get("ErrorFix_ServicesFixedNoReboot"));
        }

        return new InstallResult(true, LocalizationService.Get("ErrorFix_ServicesVerified"));
    }

    private static InstallResult ExecuteRestartComputer(InstallStep step, IProgress<string>? log)
    {
        if (step.ConfirmBeforeRestart)
        {
            var confirm = MessageBox.Show(
                LocalizationService.GetString("ErrorFix_RestartConfirmMessage"),
                LocalizationService.GetString("ErrorFix_RestartConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return new InstallResult(false, LocalizationService.Get("ErrorFix_RestartCancelled"));
            }
        }

        MessageBox.Show(
            LocalizationService.GetString("ErrorFix_RestartMessage"),
            LocalizationService.GetString("ErrorFix_RestartTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        ErrorFixStateService.MarkPostRebootPending(InstallSessionService.CurrentModeId);
        var registered = PostRebootAutoStartService.Register(log);
        if (!registered)
        {
            log?.Report(LocalizationService.Get("ErrorFix_AutoStartRegistrationFailed"));
        }

        var delay = step.RestartDelaySeconds > 0 ? step.RestartDelaySeconds : 10;
        ScheduleRestart(delay, log);

        return new InstallResult(true, LocalizationService.Format("ErrorFix_RestartScheduled", delay));
    }

    private static InstallResult ExecuteStopUserProcesses(InstallStep step, IProgress<string>? log)
    {
        var stopped = UserProcessStopService.StopUserApplications(log);
        return new InstallResult(
            true,
            LocalizationService.Format("UserProcessStop_Result", stopped));
    }

    private static InstallResult ExecuteEnsureServicesAutomatic(InstallStep step, IProgress<string>? log)
    {
        var services = ResolveServiceNames(step);
        if (services.Count == 0)
        {
            services =
            [
                ..RiotVanguardService.DefaultVanguardServices,
                ..RiotVanguardService.DefaultRiotServices
            ];
        }

        var configured = 0;
        foreach (var serviceName in services)
        {
            log?.Report(LocalizationService.Format("ErrorFix_SettingServiceAutomatic", serviceName));
            if (RiotVanguardService.SetServiceAutomatic(serviceName, log))
            {
                configured++;
            }
        }

        return new InstallResult(
            true,
            LocalizationService.Format("EnsureServicesAutomatic_Completed", configured, services.Count));
    }

    private static InstallResult ExecuteRunDesktopApp(InstallStep step, IProgress<string>? log)
    {
        var folderName = string.IsNullOrWhiteSpace(step.DesktopFolderName) ? "Emulator" : step.DesktopFolderName;
        var useAnyApp = ResellerSelectionService.IsOtherReseller ||
                        step.UseAnyDesktopApp ||
                        string.IsNullOrWhiteSpace(step.DesktopAppName) ||
                        step.DesktopAppName == "*";

        string? appPath;
        string notFoundLabel;

        if (useAnyApp)
        {
            appPath = DesktopAppResolver.FindAnyDesktopApp(folderName);
            notFoundLabel = LocalizationService.Get("ErrorFix_AnyAppNotFound");
        }
        else
        {
            var appName = string.IsNullOrWhiteSpace(step.DesktopAppName) ? "UNKCLUB.exe" : step.DesktopAppName;
            appPath = DesktopAppResolver.FindDesktopApp(folderName, appName);
            notFoundLabel = LocalizationService.Format("ErrorFix_UnkclubNotFound", folderName, appName);
        }

        if (appPath is null || !File.Exists(appPath))
        {
            if (!useAnyApp &&
                (step.DownloadFromGitHub ||
                 string.Equals(step.DesktopAppName, UpdateConstants.UnkclubAppFileName, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var destinationPath = UnkclubAppService.ResolveDestinationPath(step);
                    appPath = UnkclubAppService
                        .DownloadToPathAsync(destinationPath, log, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    return new InstallResult(false, LocalizationService.Format("UnkclubApp_DeployFailed", ex.Message));
                }
            }
            else
            {
                return new InstallResult(false, notFoundLabel);
            }
        }

        if (appPath is null || !File.Exists(appPath))
        {
            return new InstallResult(false, notFoundLabel);
        }

        log?.Report(LocalizationService.Format("ErrorFix_LaunchingProgram", appPath));
        SystemChecks.UnblockFile(appPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(appPath) ?? Environment.CurrentDirectory
        });

        var titleKey = ResolveDesktopAppMessageKey(step, useAnyApp, isTitle: true);
        var messageKey = ResolveDesktopAppMessageKey(step, useAnyApp, isTitle: false);

        MessageBox.Show(
            LocalizationService.GetString(messageKey),
            LocalizationService.GetString(titleKey),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        ErrorFixStateService.MarkComplete();

        return new InstallResult(
            true,
            useAnyApp
                ? LocalizationService.Get("ErrorFix_AppLaunched")
                : LocalizationService.Get("ErrorFix_UnkclubLaunched"));
    }

    private static string ResolveDesktopAppMessageKey(InstallStep step, bool useAnyApp, bool isTitle)
    {
        if (isTitle)
        {
            if (!string.IsNullOrWhiteSpace(step.MessageTitleKey))
            {
                return step.MessageTitleKey;
            }

            return useAnyApp
                ? "ErrorFix_AppFollowStepsTitle"
                : "ErrorFix_UnkclubFollowStepsTitle";
        }

        if (!string.IsNullOrWhiteSpace(step.MessageKey))
        {
            return step.MessageKey;
        }

        return useAnyApp
            ? "ErrorFix_AppFollowSteps"
            : "ErrorFix_UnkclubFollowSteps";
    }

    private static void ShowLocalizedMessage(InstallStep step)
    {
        if (string.IsNullOrWhiteSpace(step.MessageKey))
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(step.MessageTitleKey)
            ? LocalizationService.GetString("ErrorFix_InfoTitle")
            : LocalizationService.GetString(step.MessageTitleKey);

        MessageBox.Show(
            LocalizationService.GetString(step.MessageKey),
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void ScheduleRestart(int delaySeconds, IProgress<string>? log)
    {
        log?.Report(LocalizationService.Format("ErrorFix_RestartScheduled", delaySeconds));

        SystemChecks.RunProcess(
            "shutdown.exe",
            $"/r /t {delaySeconds} /c \"UNKCLUB Tool\"",
            null,
            [0],
            30,
            waitForExit: false);
    }

    private static List<string> ResolveServiceNames(InstallStep step)
    {
        if (step.Services.Count > 0)
        {
            return step.Services;
        }

        return [];
    }

    private static List<string> ResolveProcessNames(InstallStep step)
    {
        if (step.ProcessNames.Count > 0)
        {
            return step.ProcessNames;
        }

        return RiotVanguardService.DefaultRiotProcesses.ToList();
    }
}
