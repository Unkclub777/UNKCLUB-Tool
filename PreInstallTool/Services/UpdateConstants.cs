namespace PreInstallTool.Services;

/// <summary>
/// GitHub repository used for release checks and downloads.
/// </summary>
public static class UpdateConstants
{
    public const string GitHubOwner = "Unkclub777";
    public const string GitHubRepo = "UNKCLUB-Tool";
    public const string DefaultBranch = "master";
    public const string LocalAppFolderName = "UNKCLUB-Tool";
    public const string ReleaseExecutableFileName = "UNKCLUB Tool.exe";
    public const string ReleaseAssetFileName = "UNKCLUB-Tool.zip";
    public const string InstallersBundleFileName = "installers-bundle.zip";
    public const string UnkclubAppFileName = "UNKCLUB.exe";
    public const string DesktopDeployFolderName = "unkclub(new)";

    /// <summary>
    /// GitHub may store spaced release filenames as dotted names (e.g. UNKCLUB.Tool.exe).
    /// </summary>
    public static readonly string[] ReleaseExecutableFileNames =
    [
        ReleaseExecutableFileName,
        "UNKCLUB.Tool.exe",
        "PreInstallTool.exe"
    ];
}
