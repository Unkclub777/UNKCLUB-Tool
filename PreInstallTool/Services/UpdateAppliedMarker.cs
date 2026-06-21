using System.IO;
using System.Text.Json;

namespace PreInstallTool.Services;

/// <summary>
/// Tracks pending and successfully applied update versions to prevent repeated update prompts.
/// </summary>
internal static class UpdateAppliedMarker
{
    private static string MarkerFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            UpdateConstants.LocalAppFolderName,
            "update-applied.json");

    public static bool ShouldSkipUpdateCheck(Version remoteVersion)
    {
        var entry = TryRead();
        if (entry is null)
        {
            return false;
        }

        if (TryParseStoredVersion(entry.AppliedVersion, out var applied) &&
            AutoUpdateService.IsAtLeastVersion(applied, remoteVersion))
        {
            UpdateDiagnostics.LogWarning(
                $"Skipping update check: v{remoteVersion} already applied (marker v{applied}).");
            return true;
        }

        if (TryParseStoredVersion(entry.PendingVersion, out var pending) &&
            AutoUpdateService.IsAtLeastVersion(pending, remoteVersion) &&
            TryParsePendingUtc(entry.PendingUtc, out var pendingUtc) &&
            DateTimeOffset.UtcNow - pendingUtc < TimeSpan.FromDays(7))
        {
            UpdateDiagnostics.LogWarning(
                $"Skipping update check: v{remoteVersion} update already pending since {pendingUtc:u}.");
            return true;
        }

        return false;
    }

    public static void RecordPending(Version remoteVersion, string targetExecutablePath)
    {
        var entry = TryRead() ?? new MarkerEntry();
        entry.PendingVersion = remoteVersion.ToString(3);
        entry.PendingUtc = DateTimeOffset.UtcNow.ToString("O");
        entry.TargetExecutablePath = targetExecutablePath;
        Write(entry);
    }

    public static void RecordApplied(Version remoteVersion, string targetExecutablePath)
    {
        var entry = TryRead() ?? new MarkerEntry();
        entry.AppliedVersion = remoteVersion.ToString(3);
        entry.AppliedUtc = DateTimeOffset.UtcNow.ToString("O");
        entry.TargetExecutablePath = targetExecutablePath;
        entry.PendingVersion = null;
        entry.PendingUtc = null;
        Write(entry);
    }

    public static string GetMarkerFilePath() => MarkerFilePath;

    private static MarkerEntry? TryRead()
    {
        try
        {
            if (!File.Exists(MarkerFilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<MarkerEntry>(File.ReadAllText(MarkerFilePath));
        }
        catch
        {
            return null;
        }
    }

    private static void Write(MarkerEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(MarkerFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(MarkerFilePath, JsonSerializer.Serialize(entry));
        }
        catch (Exception ex)
        {
            UpdateDiagnostics.LogWarning($"Could not write update marker: {ex.Message}");
        }
    }

    private static bool TryParseStoredVersion(string? value, out Version version) =>
        AutoUpdateService.TryParseVersion(value, out version);

    private static bool TryParsePendingUtc(string? value, out DateTimeOffset pendingUtc) =>
        DateTimeOffset.TryParse(value, out pendingUtc);

    internal sealed class MarkerEntry
    {
        public string? AppliedVersion { get; set; }
        public string? AppliedUtc { get; set; }
        public string? PendingVersion { get; set; }
        public string? PendingUtc { get; set; }
        public string? TargetExecutablePath { get; set; }
    }
}
