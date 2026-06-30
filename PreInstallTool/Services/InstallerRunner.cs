using System.Diagnostics;
using System.IO;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class InstallerRunner
{
    public static InstallResult Run(string filePath, InstallStep parentStep, IProgress<string>? log)
    {
        SystemChecks.UnblockFile(filePath);

        var profile = InstallerCatalog.GetProfile(filePath);
        var fileName = Path.GetFileName(filePath);

        if (InstallerCatalog.ShouldSkip(filePath))
        {
            log?.Report(LocalizationService.Format("InstallerAlreadyInstalledSkip", fileName));
            return AlreadyInstalledResult(fileName);
        }

        var successCodes = MergeSuccessCodes(parentStep.SuccessExitCodes, profile.SuccessExitCodes);
        var arguments = string.IsNullOrWhiteSpace(parentStep.InstallerArguments)
            ? profile.Arguments
            : parentStep.InstallerArguments;

        log?.Report(LocalizationService.Format("InstallerInstallingFile", fileName));

        return profile.RunMode switch
        {
            InstallerRunMode.DirectXWizard =>
                DirectXInstallerAutomation.Run(filePath, log, parentStep.TimeoutSeconds),
            _ => RunSilentInstaller(
                filePath,
                arguments,
                successCodes,
                parentStep.TimeoutSeconds,
                parentStep.WaitForExit,
                log)
        };
    }

    private static InstallResult RunSilentInstaller(
        string filePath,
        string arguments,
        IReadOnlyList<int> successCodes,
        int timeoutSeconds,
        bool waitForExit,
        IProgress<string>? log)
    {
        var workingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;

        using var uiMonitor = StartDialogMonitor(log, timeoutSeconds);

        var result = SystemChecks.RunProcess(
            filePath,
            arguments,
            workingDirectory,
            successCodes,
            timeoutSeconds,
            waitForExit);

        uiMonitor.Cancel();

        if (result.Success)
        {
            return new InstallResult(true, LocalizationService.Get("InstallCompleted"));
        }

        log?.Report(LocalizationService.Format("SilentInstallTryingUi", result.ExitCode));
        return TryUiFallback(filePath, arguments, successCodes, timeoutSeconds, log, result.ExitCode);
    }

    private static InstallResult TryUiFallback(
        string filePath,
        string arguments,
        IReadOnlyList<int> successCodes,
        int timeoutSeconds,
        IProgress<string>? log,
        int previousExitCode)
    {
        if (successCodes.Contains(previousExitCode))
        {
            return new InstallResult(true, LocalizationService.Get("InstallCompletedAlreadyInstalled"));
        }

        if (TryAlreadyInstalledResult(filePath, log) is { } installed)
        {
            return installed;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (InstallerUiAutomation.TryDismissCommonDialogs(log))
            {
                Thread.Sleep(1000);
            }
        }

        var retry = SystemChecks.RunProcess(
            filePath,
            arguments,
            Path.GetDirectoryName(filePath),
            successCodes,
            Math.Min(timeoutSeconds, 300),
            true);

        if (retry.Success)
        {
            return new InstallResult(true, LocalizationService.Get("InstallCompleted"));
        }

        if (successCodes.Contains(retry.ExitCode))
        {
            return new InstallResult(true, LocalizationService.Get("InstallCompletedAlreadyInstalled"));
        }

        if (TryAlreadyInstalledResult(filePath, log) is { } installedAfterRetry)
        {
            return installedAfterRetry;
        }

        return new InstallResult(false, LocalizationService.Format("InstallFailed", retry.ExitCode));
    }

    private static InstallResult? TryAlreadyInstalledResult(string filePath, IProgress<string>? log)
    {
        if (!InstallerCatalog.ShouldSkip(filePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(filePath);
        log?.Report(LocalizationService.Format("InstallerAlreadyInstalledSkip", fileName));
        return AlreadyInstalledResult(fileName);
    }

    private static InstallResult AlreadyInstalledResult(string fileName) =>
        new(
            true,
            LocalizationService.Format("InstallerAlreadyInstalledResult", fileName),
            skipped: true);

    private static CancellationTokenSource StartDialogMonitor(IProgress<string>? log, int timeoutSeconds)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                InstallerUiAutomation.TryDismissCommonDialogs(log);
                try
                {
                    await Task.Delay(1500, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);

        return cts;
    }

    private static int[] MergeSuccessCodes(IReadOnlyList<int> fromStep, IReadOnlyList<int> fromProfile) =>
        fromStep.Concat(fromProfile).Distinct().ToArray();
}
