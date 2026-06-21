using PreInstallTool.Services;

namespace PreInstallTool.Tests;

public class AutoUpdateServiceTests
{
    [Theory]
    [InlineData("1.6.2", "1.6.1", true)]
    [InlineData("1.6.1", "1.6.2", false)]
    [InlineData("v1.6.2", "1.6.2.0", true)]
    [InlineData("1.6.2", "1.6.2", true)]
    public void TryParseVersion_SupportsVersionComparison(string left, string right, bool leftIsGreaterOrEqual)
    {
        Assert.True(AutoUpdateService.TryParseVersion(left, out var leftVersion));
        Assert.True(AutoUpdateService.TryParseVersion(right, out var rightVersion));

        Assert.Equal(leftIsGreaterOrEqual, leftVersion >= rightVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void TryParseVersion_RejectsInvalidValues(string value)
    {
        Assert.False(AutoUpdateService.TryParseVersion(value, out _));
    }
}
