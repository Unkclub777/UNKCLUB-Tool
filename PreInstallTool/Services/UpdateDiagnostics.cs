using System.IO;

namespace PreInstallTool.Services;

internal static class UpdateDiagnostics
{
    private static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            UpdateConstants.LocalAppFolderName,
            "update.log");

    public static void LogWarning(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [WARN] {message}{Environment.NewLine}";
            File.AppendAllText(LogFilePath, line);
        }
        catch
        {
            // Best effort only.
        }

        System.Diagnostics.Debug.WriteLine($"[UNKCLUB Update] {message}");
    }
}
