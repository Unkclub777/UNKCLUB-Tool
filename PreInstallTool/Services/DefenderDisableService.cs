using System.Text;
using System.Windows;
using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

/// <summary>
/// Disables Windows Defender and SmartScreen using registry, PowerShell,
/// service and MpCmdRun blocking. Exclusions are applied first.
/// </summary>
public static class DefenderDisableService
{
    public const int MaxVerifyRetries = 5;
    public const int VerifyDelayMs = 2000;

    private const string TamperDisableScript = """
        $ErrorActionPreference = 'SilentlyContinue'

        if (Get-Command Set-MpPreference -ErrorAction SilentlyContinue) {
            Set-MpPreference -DisableTamperProtection $true -ErrorAction SilentlyContinue
        }

        $policyFeatures = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Features'
        New-Item -Path $policyFeatures -Force | Out-Null
        Set-ItemProperty -Path $policyFeatures -Name TamperProtection -Value 0 -Type DWord -Force

        $runtimeFeatures = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Features'
        New-Item -Path $runtimeFeatures -Force | Out-Null
        Set-ItemProperty -Path $runtimeFeatures -Name TamperProtection -Value 0 -Type DWord -Force

        exit 0
        """;

    private const string CloudDisableScript = """
        $ErrorActionPreference = 'SilentlyContinue'

        if (Get-Command Set-MpPreference -ErrorAction SilentlyContinue) {
            Set-MpPreference `
                -MAPSReporting Disabled `
                -SubmitSamplesConsent NeverSend `
                -DisableBlockAtFirstSeen $true `
                -ErrorAction SilentlyContinue
        }

        $spynetPolicy = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet'
        New-Item -Path $spynetPolicy -Force | Out-Null
        Set-ItemProperty -Path $spynetPolicy -Name SpynetReporting -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $spynetPolicy -Name SubmitSamplesConsent -Value 2 -Type DWord -Force

        $spynetRuntime = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Spynet'
        New-Item -Path $spynetRuntime -Force | Out-Null
        Set-ItemProperty -Path $spynetRuntime -Name SpynetReporting -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $spynetRuntime -Name SubmitSamplesConsent -Value 2 -Type DWord -Force

        exit 0
        """;

    private const string DisableScript = """
        $ErrorActionPreference = 'SilentlyContinue'

        if (Get-Command Set-MpPreference -ErrorAction SilentlyContinue) {
            Set-MpPreference `
                -DisableTamperProtection $true `
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

        $rtpPolicy = Join-Path $dp 'Real-Time Protection'
        New-Item -Path $rtpPolicy -Force | Out-Null
        Set-ItemProperty -Path $rtpPolicy -Name DisableRealtimeMonitoring -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpPolicy -Name DisableBehaviorMonitoring -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpPolicy -Name DisableOnAccessProtection -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpPolicy -Name DisableScanOnRealtimeEnable -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpPolicy -Name DisableIOAVProtection -Value 1 -Type DWord -Force

        $rtpRuntime = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection'
        New-Item -Path $rtpRuntime -Force | Out-Null
        Set-ItemProperty -Path $rtpRuntime -Name DisableRealtimeMonitoring -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpRuntime -Name DisableBehaviorMonitoring -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpRuntime -Name DisableOnAccessProtection -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpRuntime -Name DisableScanOnRealtimeEnable -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $rtpRuntime -Name DisableIOAVProtection -Value 1 -Type DWord -Force

        $spynetPolicy = Join-Path $dp 'Spynet'
        New-Item -Path $spynetPolicy -Force | Out-Null
        Set-ItemProperty -Path $spynetPolicy -Name SpynetReporting -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $spynetPolicy -Name SubmitSamplesConsent -Value 2 -Type DWord -Force

        $spynetRuntime = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Spynet'
        New-Item -Path $spynetRuntime -Force | Out-Null
        Set-ItemProperty -Path $spynetRuntime -Name SpynetReporting -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $spynetRuntime -Name SubmitSamplesConsent -Value 2 -Type DWord -Force

        $mpEnginePolicy = Join-Path $dp 'MpEngine'
        New-Item -Path $mpEnginePolicy -Force | Out-Null
        Set-ItemProperty -Path $mpEnginePolicy -Name MpEnablePus -Value 0 -Type DWord -Force

        $mpEngineRuntime = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\MpEngine'
        New-Item -Path $mpEngineRuntime -Force | Out-Null
        Set-ItemProperty -Path $mpEngineRuntime -Name MpEnablePus -Value 0 -Type DWord -Force

        $puaPolicy = Join-Path $dp 'PUA Protection'
        New-Item -Path $puaPolicy -Force | Out-Null
        Set-ItemProperty -Path $puaPolicy -Name PUAProtection -Value 0 -Type DWord -Force

        $featuresPolicy = Join-Path $dp 'Features'
        New-Item -Path $featuresPolicy -Force | Out-Null
        Set-ItemProperty -Path $featuresPolicy -Name TamperProtection -Value 0 -Type DWord -Force

        $featuresRuntime = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Features'
        New-Item -Path $featuresRuntime -Force | Out-Null
        Set-ItemProperty -Path $featuresRuntime -Name TamperProtection -Value 0 -Type DWord -Force

        $networkPolicy = Join-Path $dp 'Windows Defender Exploit Guard\Network Protection'
        New-Item -Path $networkPolicy -Force | Out-Null
        Set-ItemProperty -Path $networkPolicy -Name EnableNetworkProtection -Value 0 -Type DWord -Force

        $networkRuntime = 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Network Protection'
        New-Item -Path $networkRuntime -Force | Out-Null
        Set-ItemProperty -Path $networkRuntime -Name EnableNetworkProtection -Value 0 -Type DWord -Force

        $secCenter = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications'
        New-Item -Path $secCenter -Force | Out-Null
        Set-ItemProperty -Path $secCenter -Name DisableNotifications -Value 1 -Type DWord -Force

        $sysPol = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System'
        New-Item -Path $sysPol -Force | Out-Null
        Set-ItemProperty -Path $sysPol -Name EnableSmartScreen -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $sysPol -Name ShellSmartScreenLevel -Value 'Off' -Type String -Force

        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' -Name SmartScreenEnabled -Value 'Off' -Type String -Force -ErrorAction SilentlyContinue

        $appHostLm = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost'
        New-Item -Path $appHostLm -Force | Out-Null
        Set-ItemProperty -Path $appHostLm -Name EnableWebContentEvaluation -Value 0 -Type DWord -Force

        $appHostCu = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost'
        New-Item -Path $appHostCu -Force | Out-Null
        Set-ItemProperty -Path $appHostCu -Name EnableWebContentEvaluation -Value 0 -Type DWord -Force

        $edgePolLm = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
        New-Item -Path $edgePolLm -Force | Out-Null
        Set-ItemProperty -Path $edgePolLm -Name SmartScreenEnabled -Value 0 -Type DWord -Force

        $edgePolCu = 'HKCU:\SOFTWARE\Policies\Microsoft\Edge'
        New-Item -Path $edgePolCu -Force | Out-Null
        Set-ItemProperty -Path $edgePolCu -Name SmartScreenEnabled -Value 0 -Type DWord -Force

        $edgeLegacy = 'HKLM:\SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter'
        New-Item -Path $edgeLegacy -Force | Out-Null
        Set-ItemProperty -Path $edgeLegacy -Name EnabledV9 -Value 0 -Type DWord -Force

        foreach ($svc in @('WinDefend','WdFilter','WdNisDrv','WdNisSvc','WdBoot','Sense','SecurityHealthService')) {
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

        log?.Report(LocalizationService.Get("DefenderDisabling"));
        RunPowerShell(TamperDisableScript, 60, log);

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
