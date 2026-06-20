namespace PreInstallTool.Models;

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    CheckFailed
}

public sealed record UpdateCheckResult(
    UpdateStatus Status,
    Version? RemoteVersion = null,
    string? DownloadUrl = null,
    string? ReleaseNotes = null,
    string? ErrorMessage = null);

public sealed record UpdateApplyResult(bool Success, string? ErrorMessage = null);
