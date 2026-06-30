using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class DirectXInstallerAutomation
{
    private static readonly string[] WindowTitleHints =
    [
        "DirectX",
        "DirectX(R)",
        "Microsoft(R) DirectX",
        "Installing Microsoft"
    ];

    private static readonly int[] SuccessExitCodes = [0, -9, 2, 3010, 1638, 1618];

    public static InstallResult Run(string exePath, IProgress<string>? log, int timeoutSeconds)
    {
        SystemChecks.UnblockFile(exePath);
        log?.Report(LocalizationService.Get("DirectXWizardStarting"));

        if (IsDirectXRuntimeInstalled())
        {
            log?.Report(LocalizationService.Get("DirectXAlreadyInstalledSkip"));
            return new InstallResult(
                true,
                LocalizationService.Get("DirectXAlreadyInstalledResult"),
                skipped: true);
        }

        using var automation = new UIA3Automation();
        using var app = Application.Launch(exePath);

        try
        {
            app.WaitWhileMainHandleIsMissing(TimeSpan.FromMinutes(2));

            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            var lastAction = DateTime.UtcNow;

            while (DateTime.UtcNow < deadline)
            {
                if (app.HasExited)
                {
                    break;
                }

                var window = FindInstallerWindow(app, automation);
                if (window is null)
                {
                    InstallerUiAutomation.TryDismissCommonDialogs(log);
                    Thread.Sleep(500);
                    continue;
                }

                var title = InstallerUiAutomation.GetWindowTitle(window);
                log?.Report(LocalizationService.Format("DirectXWindowTitle", title));

                if (InstallerUiAutomation.TryClickFinish(window, log))
                {
                    lastAction = DateTime.UtcNow;
                    Thread.Sleep(1500);
                    continue;
                }

                InstallerUiAutomation.SelectAcceptAgreement(window, log);
                InstallerUiAutomation.EnsureBingBarUnchecked(window, log);

                if (InstallerUiAutomation.TryClickNext(window, log))
                {
                    lastAction = DateTime.UtcNow;
                    Thread.Sleep(1200);
                    continue;
                }

                if (InstallerUiAutomation.IsProgressPage(window))
                {
                    log?.Report(LocalizationService.Get("DirectXDownloadingWaiting"));
                    Thread.Sleep(2000);
                    continue;
                }

                if ((DateTime.UtcNow - lastAction).TotalSeconds > 15)
                {
                    InstallerUiAutomation.TryDismissCommonDialogs(log);
                    lastAction = DateTime.UtcNow;
                }

                Thread.Sleep(800);
            }

            Process process;
            try
            {
                process = Process.GetProcessById(app.ProcessId);
            }
            catch
            {
                return IsDirectXRuntimeInstalled()
                    ? new InstallResult(true, LocalizationService.Get("DirectXInstallCompleted"))
                    : new InstallResult(false, LocalizationService.Get("DirectXProcessNotFound"));
            }

            if (!process.HasExited)
            {
                log?.Report(LocalizationService.Get("DirectXWaitingCompletion"));
                process.WaitForExit(Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds));
            }

            var exitCode = process.HasExited ? process.ExitCode : -1;
            log?.Report(LocalizationService.Format("DirectXInstallExitCode", exitCode));

            if (SuccessExitCodes.Contains(exitCode) || IsDirectXRuntimeInstalled())
            {
                return new InstallResult(true, LocalizationService.Get("DirectXInstallCompleted"));
            }

            return new InstallResult(false, LocalizationService.Format("DirectXInstallFailed", exitCode));
        }
        catch (Exception ex)
        {
            if (IsDirectXRuntimeInstalled())
            {
                log?.Report(LocalizationService.Get("DirectXAlreadyInstalledSkip"));
                return new InstallResult(
                    true,
                    LocalizationService.Get("DirectXAlreadyInstalledResult"),
                    skipped: true);
            }

            return new InstallResult(false, LocalizationService.Format("DirectXAutomationError", ex.Message));
        }
    }

    private static bool IsDirectXRuntimeInstalled() => SystemChecks.IsDirectX9RuntimeInstalled();

    private static AutomationElement? FindInstallerWindow(Application app, UIA3Automation automation)
    {
        foreach (var window in app.GetAllTopLevelWindows(automation))
        {
            var title = InstallerUiAutomation.GetWindowTitle(window);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (WindowTitleHints.Any(hint =>
                    title.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            {
                return window;
            }
        }

        return null;
    }
}
