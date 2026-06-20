using System.IO;
using System.Text;
using System.Windows;

namespace PreInstallTool.Services;

public static class InstallLogService
{
    public static string BuildLogText(IEnumerable<string> logLines)
    {
        var builder = new StringBuilder();
        foreach (var line in logLines)
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    public static void CopyToClipboard(IEnumerable<string> logLines)
    {
        var text = BuildLogText(logLines);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Clipboard.SetText(text);
    }

    public static string SaveToDesktopEmulatorFolder(IEnumerable<string> logLines)
    {
        var emulatorFolder = DesktopPathService.GetEmulatorFolderPath();
        Directory.CreateDirectory(emulatorFolder);

        var fileName = $"install-log-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        var targetPath = Path.Combine(emulatorFolder, fileName);
        File.WriteAllText(targetPath, BuildLogText(logLines), Encoding.UTF8);
        return targetPath;
    }
}
