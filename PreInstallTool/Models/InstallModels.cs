using System.Text.Json.Serialization;

namespace PreInstallTool.Models;

public sealed class InstallConfig
{
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = "Kurulum Aracı";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("mainProgramPath")]
    public string? MainProgramPath { get; set; }

    [JsonPropertyName("mainProgramArgs")]
    public string? MainProgramArgs { get; set; }

    [JsonPropertyName("launchMainProgramWhenDone")]
    public bool LaunchMainProgramWhenDone { get; set; }

    [JsonPropertyName("modes")]
    public List<InstallMode> Modes { get; set; } = [];

    [JsonPropertyName("steps")]
    public List<InstallStep> Steps { get; set; } = [];
}

public sealed class InstallMode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<InstallStep> Steps { get; set; } = [];
}

public sealed class InstallStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "checkCommand";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("optional")]
    public bool Optional { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    [JsonPropertyName("successExitCodes")]
    public List<int> SuccessExitCodes { get; set; } = [0];

    [JsonPropertyName("successPattern")]
    public string? SuccessPattern { get; set; }

    [JsonPropertyName("failurePattern")]
    public string? FailurePattern { get; set; }

    [JsonPropertyName("pathExists")]
    public string? PathExists { get; set; }

    [JsonPropertyName("registryKey")]
    public string? RegistryKey { get; set; }

    [JsonPropertyName("registryValueName")]
    public string? RegistryValueName { get; set; }

    [JsonPropertyName("registryExpectedValue")]
    public string? RegistryExpectedValue { get; set; }

    [JsonPropertyName("environmentVariable")]
    public string? EnvironmentVariable { get; set; }

    [JsonPropertyName("environmentExpectedContains")]
    public string? EnvironmentExpectedContains { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("downloadFileName")]
    public string? DownloadFileName { get; set; }

    [JsonPropertyName("installerArguments")]
    public string? InstallerArguments { get; set; }

    [JsonPropertyName("folderPath")]
    public string? FolderPath { get; set; }

    [JsonPropertyName("sourcePath")]
    public string? SourcePath { get; set; }

    [JsonPropertyName("destinationPath")]
    public string? DestinationPath { get; set; }

    [JsonPropertyName("destinationFolder")]
    public string? DestinationFolder { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("duplicateFolderStrategy")]
    public string? DuplicateFolderStrategy { get; set; }

    [JsonPropertyName("fileExtensions")]
    public List<string> FileExtensions { get; set; } = [".exe", ".msi", ".bat", ".cmd"];

    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; }

    [JsonPropertyName("runAsAdmin")]
    public bool RunAsAdmin { get; set; }

    [JsonPropertyName("waitForExit")]
    public bool WaitForExit { get; set; } = true;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 600;

    [JsonPropertyName("skipIf")]
    public SkipCondition? SkipIf { get; set; }

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("services")]
    public List<string> Services { get; set; } = [];

    [JsonPropertyName("processNames")]
    public List<string> ProcessNames { get; set; } = [];

    [JsonPropertyName("folderPaths")]
    public List<string> FolderPaths { get; set; } = [];

    [JsonPropertyName("programPath")]
    public string? ProgramPath { get; set; }

    [JsonPropertyName("programPaths")]
    public List<string> ProgramPaths { get; set; } = [];

    [JsonPropertyName("messageKey")]
    public string? MessageKey { get; set; }

    [JsonPropertyName("messageTitleKey")]
    public string? MessageTitleKey { get; set; }

    [JsonPropertyName("desktopAppName")]
    public string? DesktopAppName { get; set; }

    [JsonPropertyName("desktopFolderName")]
    public string? DesktopFolderName { get; set; }

    [JsonPropertyName("setStartupAutomatic")]
    public bool SetStartupAutomatic { get; set; }

    [JsonPropertyName("waitTimeoutSeconds")]
    public int WaitTimeoutSeconds { get; set; } = 300;

    [JsonPropertyName("restartDelaySeconds")]
    public int RestartDelaySeconds { get; set; } = 10;

    [JsonPropertyName("stopProcesses")]
    public bool StopProcesses { get; set; }

    [JsonPropertyName("confirmBeforeRestart")]
    public bool ConfirmBeforeRestart { get; set; }

    [JsonPropertyName("useAnyDesktopApp")]
    public bool UseAnyDesktopApp { get; set; }

    [JsonPropertyName("forceReinstall")]
    public bool ForceReinstall { get; set; }

    [JsonPropertyName("downloadFromGitHub")]
    public bool DownloadFromGitHub { get; set; }
}

public sealed class SkipCondition
{
    [JsonPropertyName("pathExists")]
    public string? PathExists { get; set; }

    [JsonPropertyName("registryKey")]
    public string? RegistryKey { get; set; }

    [JsonPropertyName("registryValueName")]
    public string? RegistryValueName { get; set; }

    [JsonPropertyName("registryExpectedValue")]
    public string? RegistryExpectedValue { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("successExitCodes")]
    public List<int> SuccessExitCodes { get; set; } = [0];
}

public enum StepStatus
{
    Pending,
    Running,
    Skipped,
    Success,
    Failed
}

public sealed class StepProgress
{
    public InstallStep Step { get; init; } = null!;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string Message { get; set; } = string.Empty;
}

public sealed class InstallResult
{
    public InstallResult(bool success, string message, bool skipped = false, string? downloadedPath = null)
    {
        Success = success;
        Message = message;
        Skipped = skipped;
        DownloadedPath = downloadedPath;
    }

    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool Skipped { get; init; }
    public string? DownloadedPath { get; init; }
}
