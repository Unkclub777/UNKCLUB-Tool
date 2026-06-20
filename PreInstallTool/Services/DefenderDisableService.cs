using System.Text;
using System.Windows;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

/// <summary>
/// Disables Windows Defender and SmartScreen using the same registry, PowerShell,
/// service and MpCmdRun blocking approach as Defender Control (dControl.ini).
/// Exclusions are applied first to prevent false-positive detections.
/// </summary>
public static class DefenderDisableService
{
    public const int MaxVerifyRetries = 5;
    public const int VerifyDelayMs = 2000;

    private const string CloudDisableScript = """
        $ErrorActionPreference = 'SilentlyContinue'

        if (Get-Command Set-MpPreference -ErrorAction SilentlyContinue) {
            Set-MpPreference `
                -MAPSReporting Disabled `
                -SubmitSamplesConsent NeverSend `
                -DisableBlockAtFirstSeen $true `
                -ErrorAction SilentlyContinue
        }

        $spynet = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet'
        New-Item -Path $spynet -Force | Out-Null
        Set-ItemProperty -Path $spynet -Name SpynetReporting -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $spynet -Name SubmitSamplesConsent -Value 2 -Type DWord -Force

        exit 0
        """;

    private const string DisableScript = """
        $ErrorActionPreference = 'SilentlyContinue'

        if (Get-Command Set-MpPreference -ErrorAction SilentlyContinue) {
            Set-MpPreference `
                -DisableRealtimeMonitoring $true `
                -DisableIOAVProtection $true `
                -DisableBehaviorMonitoring $true `
                -DisableBlockAtFirstSeen $true `
                -DisableIntrusionPreventionSystem $true `
                -DisableScriptScanning $true `
                -DisableArchiveScanning $true `
                -DisableEmailScanning $true `
                -DisableRemovableDriveScanning $true `
                -DisableScanningMappedNetworkDrivesForFullScan $true `
                -DisableScanningNetworkFiles $true `
                -DisableCatchupFullScan $true `
                -DisableCatchupQuickScan $true `
                -PUAProtection Disabled `
                -EnableNetworkProtection Disabled `
                -MAPSReporting Disabled `
                -SubmitSamplesConsent NeverSend `
                -ErrorAction SilentlyContinue
        }

        $dp = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender'
        New-Item -Path $dp -Force | Out-Null
        Set-ItemProperty -Path $dp -Name DisableAntiSpyware -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $dp -Name DisableAntiVirus -Value 1 -Type DWord -Force

        $rtp = Join-Path $dp 'Real-Time Protection'
        New-Item -Path $rtp -Force | Out-Null
        Set-ItemProperty -Path $rtp -Name DisableRealtimeMonitoring -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtp -Name DisableBehaviorMonitoring -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtp -Name DisableOnAccessProtection -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtp -Name DisableScanOnRealtimeEnable -Value 1 -Type DWord -Force

        $spynet = Join-Path $dp 'Spynet'
        New-Item -Path $spynet -Force | Out-Null
        Set-ItemProperty -Path $spynet -Name SpynetReporting -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $spynet -Name SubmitSamplesConsent -Value 2 -Type DWord -Force

        $mpEngine = Join-Path $dp 'MpEngine'
        New-Item -Path $mpEngine -Force | Out-Null
        Set-ItemProperty -Path $mpEngine -Name MpEnablePus -Value 0 -Type DWord -Force

        $features = Join-Path $dp 'Features'
        New-Item -Path $features -Force | Out-Null
        Set-ItemProperty -Path $features -Name TamperProtection -Value 0 -Type DWord -Force
        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Features' -Name TamperProtection -Value 0 -Type DWord -Force

        $secCenter = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications'
        New-Item -Path $secCenter -Force | Out-Null
        Set-ItemProperty -Path $secCenter -Name DisableNotifications -Value 1 -Type DWord -Force

        $sysPol = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System'
        New-Item -Path $sysPol -Force | Out-Null
        Set-ItemProperty -Path $sysPol -Name EnableSmartScreen -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $sysPol -Name ShellSmartScreenLevel -Value 'Off' -Type String -Force

        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' -Name SmartScreenEnabled -Value 'Off' -Type String -Force -ErrorAction SilentlyContinue

        $edgePol = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
        New-Item -Path $edgePol -Force | Out-Null
        Set-ItemProperty -Path $edgePol -Name SmartScreenEnabled -Value 0 -Type DWord -Force
        $edgeLegacy = 'HKLM:\SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter'
        New-Item -Path $edgeLegacy -Force | Out-Null
        Set-ItemProperty -Path $edgeLegacy -Name EnabledV9 -Value 0 -Type DWord -Force

        foreach ($svc in @('WinDefend','WdFilter','WdNisDrv','WdNisSvc','Sense','SecurityHealthService')) {
            $sk = 'HKLM:\SYSTEM\CurrentControlSet\Services\' + $svc
            if (Test-Path $sk) {
                Set-ItemProperty -Path $sk -Name Start -Value 4 -Type DWord -Force
                Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
                & sc.exe config $svc start= disabled | Out-Null
                & sc.exe stop $svc | Out-Null
            }
        }

        $mpcmd = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'
        if (Test-Path $mpcmd) {
            & icacls $mpcmd /deny 'Everyone:(RX)' | Out-Null
        }

        exit 0
        """;

    public static InstallResult Disable(IProgress<string>? log = null, Action? refreshStatus = null)
    {
        DefenderExclusionService.AddExclusions(log);

        log?.Report(LocalizationService.Get("DefenderCloudDisabling"));
        RunPowerShell(CloudDisableScript, 60, log);

        ShowExclusionInfoMessage();

        InstallResult? lastDisableResult = null;

        for (var attempt = 1; attempt <= MaxVerifyRetries; attempt++)
        {
            log?.Report(LocalizationService.Get("DefenderDisabling"));
            lastDisableResult = RunFullDisable(log);

            refreshStatus?.Invoke();
            Thread.Sleep(VerifyDelayMs);
            refreshStatus?.Invoke();

            if (!DefenderStatusService.AnyFeatureEnabled())
            {
                log?.Report(LocalizationService.Get("DefenderApplied"));
                return new InstallResult(true, LocalizationService.Get("DefenderDisabled"));
            }

            if (attempt < MaxVerifyRetries)
            {
                log?.Report(LocalizationService.Format("DefenderVerifyRetry", attempt, MaxVerifyRetries));
            }
        }

        var enabledFeatures = DefenderStatusService.GetEnabledFeatureIds();
        var featureList = string.Join(", ", enabledFeatures);
        log?.Report(LocalizationService.Format("DefenderVerifyWarning", featureList));

        if (lastDisableResult?.Success == true)
        {
            return new InstallResult(true, LocalizationService.Format("DefenderPartialActive", featureList));
        }

        log?.Report(LocalizationService.Format(
            "DefenderDisableWarning",
            lastDisableResult is null ? -1 : 0));

        return new InstallResult(true, LocalizationService.Format("DefenderPartialActive", featureList));
    }

    private static InstallResult RunFullDisable(IProgress<string>? log)
    {
        var result = RunPowerShell(DisableScript, 180, log);

        if (result.Success)
        {
            return new InstallResult(true, LocalizationService.Get("DefenderDisabled"));
        }

        log?.Report(LocalizationService.Format("DefenderDisableWarning", result.ExitCode));
        return new InstallResult(true, LocalizationService.Get("DefenderPartial"));
    }

    private static void ShowExclusionInfoMessage()
    {
        try
        {
            var show = () => MessageBox.Show(
                LocalizationService.GetString("DefenderExclusionsInfoMessage"),
                LocalizationService.GetString("DefenderExclusionsInfoTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(show);
            }
            else
            {
                show();
            }
        }
        catch
        {
            // Non-fatal if UI message cannot be shown.
        }
    }

    private static (bool Success, int ExitCode) RunPowerShell(
        string script,
        int timeoutSeconds,
        IProgress<string>? log = null)
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
