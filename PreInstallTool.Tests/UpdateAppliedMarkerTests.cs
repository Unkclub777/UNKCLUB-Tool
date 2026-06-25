using System.Text.Json;
using PreInstallTool.Services;

namespace PreInstallTool.Tests;

public class UpdateAppliedMarkerTests
{
    [Fact]
    public void MarkerJson_DeserializesCamelCaseFromUpdaterScript()
    {
        const string json =
            """
            {"appliedVersion":"1.6.5","appliedUtc":"2026-06-25T12:00:00.0000000+00:00","targetExecutablePath":"C:\\Apps\\UNKCLUB Tool.exe"}
            """;

        var entry = JsonSerializer.Deserialize<UpdateAppliedMarker.MarkerEntry>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(entry);
        Assert.Equal("1.6.5", entry!.AppliedVersion);
        Assert.Equal("C:\\Apps\\UNKCLUB Tool.exe", entry.TargetExecutablePath);
    }
}
