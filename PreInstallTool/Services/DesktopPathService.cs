using System.IO;

namespace PreInstallTool.Services;

/// <summary>
/// Resolves the physical desktop directory consistently (OneDrive Desktop vs %USERPROFILE%\Desktop).
/// </summary>
public static class DesktopPathService
{
    public static string GetDesktopDirectory()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
        {
            return Path.GetFullPath(desktop);
        }

        desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
        {
            return Path.GetFullPath(desktop);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var candidate in GetDesktopCandidates(userProfile))
        {
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return Path.GetFullPath(Path.Combine(userProfile, "Desktop"));
    }

    public static string NormalizeDesktopPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        var desktop = GetDesktopDirectory();
        expanded = expanded.Replace("%DESKTOP%", desktop, StringComparison.OrdinalIgnoreCase);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var legacyPrefix in GetLegacyDesktopPrefixes(userProfile))
        {
            if (!legacyPrefix.Equals(desktop, StringComparison.OrdinalIgnoreCase) &&
                expanded.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                expanded = desktop + expanded[legacyPrefix.Length..];
                break;
            }
        }

        return Path.GetFullPath(expanded);
    }

    public static string GetEmulatorFolderPath(string folderName = "Emulator") =>
        Path.Combine(GetDesktopDirectory(), folderName);

    public static string ResolveUniqueDestinationFolder(string destinationFolder, string? duplicateFolderStrategy)
    {
        var parentDirectory = Path.GetDirectoryName(destinationFolder);
        var folderName = Path.GetFileName(destinationFolder);

        if (string.IsNullOrWhiteSpace(parentDirectory) || string.IsNullOrWhiteSpace(folderName))
        {
            return destinationFolder;
        }

        if (!Directory.Exists(destinationFolder))
        {
            return destinationFolder;
        }

        var strategy = duplicateFolderStrategy?.Trim().ToLowerInvariant() ?? "windows";
        if (strategy is not ("windows" or "increment"))
        {
            return destinationFolder;
        }

        for (var index = 2; index <= 999; index++)
        {
            var candidate = Path.Combine(parentDirectory, $"{folderName} ({index})");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(parentDirectory, $"{folderName} (New)");
    }

    private static IEnumerable<string> GetDesktopCandidates(string userProfile)
    {
        yield return Path.Combine(userProfile, "OneDrive", "Desktop");

        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrWhiteSpace(oneDrive))
        {
            yield return Path.Combine(oneDrive, "Desktop");
        }

        var oneDriveCommercial = Environment.GetEnvironmentVariable("OneDriveCommercial");
        if (!string.IsNullOrWhiteSpace(oneDriveCommercial))
        {
            yield return Path.Combine(oneDriveCommercial, "Desktop");
        }

        yield return Path.Combine(userProfile, "OneDrive - Personal", "Desktop");
        yield return Path.Combine(userProfile, "Desktop");
    }

    private static IEnumerable<string> GetLegacyDesktopPrefixes(string userProfile)
    {
        foreach (var candidate in GetDesktopCandidates(userProfile))
        {
            yield return Path.GetFullPath(candidate);
        }
    }
}
