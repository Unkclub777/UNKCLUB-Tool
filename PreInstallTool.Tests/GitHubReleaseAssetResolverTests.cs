using PreInstallTool.Services;

namespace PreInstallTool.Tests;

public class GitHubReleaseAssetResolverTests
{
    [Theory]
    [InlineData("1.6.2", "UNKCLUB Tool.exe", "https://github.com/Unkclub777/UNKCLUB-Tool/releases/download/v1.6.2/UNKCLUB%20Tool.exe")]
    [InlineData("v1.6.2", "installers-bundle.zip", "https://github.com/Unkclub777/UNKCLUB-Tool/releases/download/v1.6.2/installers-bundle.zip")]
    [InlineData("1.6.2", "UNKCLUB.Tool.exe", "https://github.com/Unkclub777/UNKCLUB-Tool/releases/download/v1.6.2/UNKCLUB.Tool.exe")]
    public void BuildDirectDownloadUrl_EncodesAssetNames(string version, string assetName, string expected)
    {
        var url = GitHubReleaseAssetResolver.BuildDirectDownloadUrl(version, assetName);

        Assert.Equal(expected, url);
    }

    [Fact]
    public void FindAssetUrl_PrefersFirstMatchingAlternateName()
    {
        var assets = new List<GitHubReleaseAssetInfo>
        {
            new("installers-bundle.zip", "https://example.com/bundle.zip"),
            new("UNKCLUB.Tool.exe", "https://example.com/dotted.exe"),
            new("UNKCLUB Tool.exe", "https://example.com/spaced.exe")
        };

        var url = GitHubReleaseAssetResolver.FindAssetUrl(
            assets,
            UpdateConstants.ReleaseExecutableFileNames);

        Assert.Equal("https://example.com/spaced.exe", url);
    }

    [Fact]
    public void FindAssetUrl_FallsBackToAlternateExecutableName()
    {
        var assets = new List<GitHubReleaseAssetInfo>
        {
            new("UNKCLUB.Tool.exe", "https://example.com/dotted.exe")
        };

        var url = GitHubReleaseAssetResolver.FindAssetUrl(
            assets,
            UpdateConstants.ReleaseExecutableFileNames);

        Assert.Equal("https://example.com/dotted.exe", url);
    }
}
