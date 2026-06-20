using System.IO;

namespace PreInstallTool.Services;

public static class DesktopAppResolver
{
    public static string? FindDesktopApp(string folderName, string fileName)
    {
        var desktop = DesktopPathService.GetDesktopDirectory();
        if (!Directory.Exists(desktop))
        {
            return null;
        }

        var candidates = EnumerateEmulatorFolders(desktop, folderName)
            .SelectMany(folder => ResolveNamedAppCandidates(folder, fileName))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(GetEmulatorFolderPriority)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .First();
    }

    public static string? FindAnyDesktopApp(string folderName)
    {
        var desktop = DesktopPathService.GetDesktopDirectory();
        if (!Directory.Exists(desktop))
        {
            return null;
        }

        var candidates = EnumerateEmulatorFolders(desktop, folderName)
            .SelectMany(folder =>
                Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly))
            .Where(IsValidEmulatorExecutable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        return candidates
            .OrderByDescending(GetEmulatorFolderPriority)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .First();
    }

    public static IEnumerable<string> EnumerateEmulatorFolders(string desktopPath, string folderName)
    {
        var exact = Path.Combine(desktopPath, folderName);
        if (Directory.Exists(exact))
        {
            yield return exact;
        }

        for (var index = 2; index <= 999; index++)
        {
            var candidate = Path.Combine(desktopPath, $"{folderName} ({index})");
            if (!Directory.Exists(candidate))
            {
                break;
            }

            yield return candidate;
        }
    }

    private static IEnumerable<string> ResolveNamedAppCandidates(string folder, string fileName)
    {
        yield return Path.Combine(folder, fileName);

        if (fileName.Equals("UNKCLUB.exe", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(folder, "UNKCLUB.exe.exe");
        }
    }

    private static bool IsValidEmulatorExecutable(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return !fileName.EndsWith(".exe.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Length > ".exe.exe".Length;
    }

    private static int GetEmulatorFolderPriority(string filePath)
    {
        var folderName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? string.Empty);
        if (folderName.Equals("Emulator", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var match = System.Text.RegularExpressions.Regex.Match(folderName, @"\((\d+)\)$");
        return match.Success && int.TryParse(match.Groups[1].Value, out var number)
            ? number
            : 0;
    }
}
