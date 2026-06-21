using PreInstallTool.Services;

namespace PreInstallTool.Tests;

public class DesktopPathServiceTests
{
    [Fact]
    public void ResolveUniqueDestinationFolder_ReturnsOriginalWhenMissing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "unkclub-test-missing-" + Guid.NewGuid().ToString("N"));

        var resolved = DesktopPathService.ResolveUniqueDestinationFolder(folder, "windows");

        Assert.Equal(folder, resolved);
    }

    [Fact]
    public void ResolveUniqueDestinationFolder_UsesIncrementStrategy()
    {
        var parent = Path.Combine(Path.GetTempPath(), "unkclub-test-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(parent, "unkclub(new)");
        Directory.CreateDirectory(original);

        try
        {
            var resolved = DesktopPathService.ResolveUniqueDestinationFolder(original, "increment");

            Assert.Equal(Path.Combine(parent, "unkclub(new) (2)"), resolved);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void NormalizeDesktopPath_ExpandsDesktopToken()
    {
        var desktop = DesktopPathService.GetDesktopDirectory();
        var normalized = DesktopPathService.NormalizeDesktopPath("%DESKTOP%\\unkclub(new)\\UNKCLUB.exe");

        Assert.Equal(Path.Combine(desktop, "unkclub(new)", "UNKCLUB.exe"), normalized);
    }
}
