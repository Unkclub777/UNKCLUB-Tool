namespace PreInstallTool.Services;

/// <summary>
/// GitHub repository used for release checks and downloads.
/// Update GitHubOwner after creating the remote repository.
/// </summary>
public static class UpdateConstants
{
    public const string GitHubOwner = "YOUR_GITHUB_USERNAME";
    public const string GitHubRepo = "UNKCLUB-Tool";
    public const string DefaultBranch = "main";
    public const string ReleaseAssetFileName = "PreInstallTool.zip";
}
