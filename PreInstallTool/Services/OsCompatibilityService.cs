using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class OsCompatibilityService
{
    public const int MinimumMajorVersion = 10;
    public const int MinimumBuildNumber = 10240;

    public static bool IsSupported()
    {
        var version = Environment.OSVersion.Version;
        return version.Major >= MinimumMajorVersion && version.Build >= MinimumBuildNumber;
    }

    public static string GetOsDescription()
    {
        var version = Environment.OSVersion.Version;
        return LocalizationService.Format("Os_VersionLabel", version.Major, version.Minor, version.Build);
    }

    public static void EnsureSupportedOrWarn()
    {
        if (IsSupported())
        {
            return;
        }

        System.Windows.MessageBox.Show(
            LocalizationService.GetString("Os_UnsupportedMessage"),
            LocalizationService.GetString("Os_UnsupportedTitle"),
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }
}
