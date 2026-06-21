using System.IO;
using System.Text.Json;

namespace PreInstallTool.Services;

/// <summary>
/// Skips update checks for 24 hours after a verified 404 on the release download URL.
/// </summary>
internal static class UpdateCheckSkipCache
{
    private static readonly TimeSpan SkipDuration = TimeSpan.FromHours(24);

    private static string CacheFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            UpdateConstants.LocalAppFolderName,
            "update-check-skip.json");

    public static bool ShouldSkip()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return false;
            }

            var json = File.ReadAllText(CacheFilePath);
            var entry = JsonSerializer.Deserialize<SkipEntry>(json);
            if (entry?.LastNotFoundUtc is null)
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(entry.LastNotFoundUtc, out var lastNotFound))
            {
                return false;
            }

            return DateTimeOffset.UtcNow - lastNotFound < SkipDuration;
        }
        catch
        {
            return false;
        }
    }

    public static void RecordNotFound()
    {
        try
        {
            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = new SkipEntry
            {
                LastNotFoundUtc = DateTimeOffset.UtcNow.ToString("O")
            };

            File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(entry));
        }
        catch
        {
            // Best effort only.
        }
    }

    private sealed class SkipEntry
    {
        public string? LastNotFoundUtc { get; set; }
    }
}
