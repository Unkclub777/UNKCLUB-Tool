using PreInstallTool.Services;

namespace PreInstallTool.Tests;

public class AutoUpdateServiceTests
{
    [Theory]
    [InlineData("1.6.2", "1.6.1", true)]
    [InlineData("1.6.1", "1.6.2", false)]
    [InlineData("v1.6.2", "1.6.2.0", true)]
    [InlineData("1.6.2", "1.6.2", true)]
    [InlineData("1.6.2", "1.6.2.0", true)]
    [InlineData("1.6.3", "1.6.3.0", true)]
    public void TryParseVersion_SupportsVersionComparison(string left, string right, bool leftIsGreaterOrEqual)
    {
        Assert.True(AutoUpdateService.TryParseVersion(left, out var leftVersion));
        Assert.True(AutoUpdateService.TryParseVersion(right, out var rightVersion));

        Assert.Equal(leftIsGreaterOrEqual, AutoUpdateService.IsAtLeastVersion(leftVersion, rightVersion));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void TryParseVersion_RejectsInvalidValues(string value)
    {
        Assert.False(AutoUpdateService.TryParseVersion(value, out _));
    }

    [Theory]
    [InlineData("1.6.2", "1.6.2.0", true)]
    [InlineData("1.6.2.0", "1.6.2", true)]
    [InlineData("1.6.3", "1.6.2", true)]
    [InlineData("1.6.1", "1.6.2", false)]
    public void IsAtLeastVersion_TreatsThreeAndFourPartVersionsAsEqual(
        string current,
        string remote,
        bool expectedUpToDate)
    {
        Assert.True(AutoUpdateService.TryParseVersion(current, out var currentVersion));
        Assert.True(AutoUpdateService.TryParseVersion(remote, out var remoteVersion));

        Assert.Equal(expectedUpToDate, AutoUpdateService.IsAtLeastVersion(currentVersion, remoteVersion));
    }

    [Fact]
    public void NormalizeVersion_PadsMissingRevisionWithZero()
    {
        Assert.True(AutoUpdateService.TryParseVersion("1.6.2", out var parsed));

        var normalized = AutoUpdateService.NormalizeVersion(parsed);

        Assert.Equal(1, normalized.Major);
        Assert.Equal(6, normalized.Minor);
        Assert.Equal(2, normalized.Build);
        Assert.Equal(0, normalized.Revision);
    }
}
