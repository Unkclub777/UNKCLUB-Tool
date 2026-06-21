using System.Diagnostics;
using System.IO;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class DesktopShortcutService
{
    private const int LinkFlagsOffset = 0x14;
    private const uint RunAsAdministratorFlag = 0x00002000;

    public static string CreateUnkclubShortcut(string targetExePath, IProgress<string>? log)
    {
        var folderPath = DesktopPathService.GetEmulatorFolderPath();
        Directory.CreateDirectory(folderPath);

        var shortcutInFolder = Path.Combine(folderPath, "UNKCLUB.lnk");
        CreateShortcut(shortcutInFolder, targetExePath);
        log?.Report(LocalizationService.Format("Shortcut_Created", shortcutInFolder));

        var desktopShortcut = Path.Combine(DesktopPathService.GetDesktopDirectory(), "UNKCLUB.lnk");
        if (!desktopShortcut.Equals(shortcutInFolder, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                CreateShortcut(desktopShortcut, targetExePath);
                log?.Report(LocalizationService.Format("Shortcut_Created", desktopShortcut));
            }
            catch (Exception ex)
            {
                log?.Report(LocalizationService.Format("Shortcut_DesktopSkipped", ex.Message));
            }
        }

        return shortcutInFolder;
    }

    private static void CreateShortcut(string shortcutPath, string targetExePath)
    {
        var targetDirectory = Path.GetDirectoryName(targetExePath)
            ?? DesktopPathService.GetEmulatorFolderPath();

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create WScript.Shell.");

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetExePath;
        shortcut.WorkingDirectory = targetDirectory;
        shortcut.Description = "UNKCLUB";
        shortcut.Save();

        SetRunAsAdministratorFlag(shortcutPath);
    }

    private static void SetRunAsAdministratorFlag(string shortcutPath)
    {
        var bytes = File.ReadAllBytes(shortcutPath);
        if (bytes.Length < LinkFlagsOffset + 4)
        {
            return;
        }

        var flags = BitConverter.ToUInt32(bytes, LinkFlagsOffset);
        flags |= RunAsAdministratorFlag;
        var flagBytes = BitConverter.GetBytes(flags);
        Buffer.BlockCopy(flagBytes, 0, bytes, LinkFlagsOffset, 4);
        File.WriteAllBytes(shortcutPath, bytes);
    }

    public static void OpenFolderInExplorer(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = true
        });
    }
}
