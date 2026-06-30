using PreInstallTool.Services;

namespace PreInstallTool.Tests;

public class InstallerCatalogTests
{
    [Fact]
    public void StandardSuccessCodes_IncludesAlreadyInstalledCodes()
    {
        Assert.Contains(1638, InstallerCatalog.StandardSuccessCodes);
        Assert.Contains(1618, InstallerCatalog.StandardSuccessCodes);
        Assert.Contains(1641, InstallerCatalog.StandardSuccessCodes);
        Assert.Contains(5101, InstallerCatalog.StandardSuccessCodes);
    }
}
