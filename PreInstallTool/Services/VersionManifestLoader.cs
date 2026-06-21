using System.Net.Http;
using System.Text.Json;

namespace PreInstallTool.Services;

public static class VersionManifestLoader
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static VersionManifestInfo? TryFetch()
    {
        var manifestUrl =
            $"https://raw.githubusercontent.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/{UpdateConstants.DefaultBranch}/version.json";

        try
        {
            var manifestJson = HttpClient.GetStringAsync(manifestUrl).GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<VersionManifestInfo>(manifestJson);
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("UNKCLUB-Tool");
        return client;
    }
}

public sealed class VersionManifestInfo
{
    public string? Version { get; set; }
    public string? InstallersBundleUrl { get; set; }
    public string? InstallersBundleVersion { get; set; }
    public string? InstallersBundleSha256 { get; set; }
    public string? UnkclubAppUrl { get; set; }
    public string? UnkclubAppSha256 { get; set; }
}
