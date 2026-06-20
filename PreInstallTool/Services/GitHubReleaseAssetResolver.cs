using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PreInstallTool.Services;

/// <summary>
/// Resolves release asset download URLs via direct GitHub release links or the Releases API.
/// </summary>
internal static class GitHubReleaseAssetResolver
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Builds the public release download URL (no API auth required for public repos/releases).
    /// </summary>
    public static string BuildDirectDownloadUrl(string versionLabel, string assetFileName)
    {
        var tag = NormalizeTag(versionLabel);
        return
            $"https://github.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/download/{tag}/{assetFileName}";
    }

    /// <summary>
    /// Resolves an asset URL via the GitHub Releases API (fallback when direct download fails).
    /// Returns null when the repo is private or the release/asset is missing.
    /// </summary>
    public static string? ResolveAssetUrl(string versionLabel, string assetFileName)
    {
        var normalizedTag = NormalizeTag(versionLabel);

        var url = TryResolveFromReleaseTag(normalizedTag, assetFileName)
                  ?? TryResolveFromLatestRelease(assetFileName);

        return url;
    }

    private static string? TryResolveFromReleaseTag(string tag, string assetFileName)
    {
        try
        {
            var apiUrl =
                $"https://api.github.com/repos/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/tags/{tag}";
            return TryResolveFromReleaseApi(apiUrl, assetFileName);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveFromLatestRelease(string assetFileName)
    {
        try
        {
            var apiUrl =
                $"https://api.github.com/repos/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/latest";
            return TryResolveFromReleaseApi(apiUrl, assetFileName);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveFromReleaseApi(string apiUrl, string assetFileName)
    {
        using var response = HttpClient.GetAsync(apiUrl).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = response.Content.ReadAsStream();
        var release = JsonSerializer.Deserialize<GitHubReleaseResponse>(stream, JsonOptions);
        return FindAssetUrl(release?.Assets, assetFileName);
    }

    private static string? FindAssetUrl(List<GitHubReleaseAssetResponse>? assets, string assetFileName)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        return assets
            .FirstOrDefault(a => a.Name.Equals(assetFileName, StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;
    }

    private static string NormalizeTag(string versionLabel)
    {
        var normalized = versionLabel.Trim();
        return normalized.StartsWith('v') || normalized.StartsWith('V')
            ? normalized
            : $"v{normalized}";
    }

    private static HttpClient CreateHttpClient()
    {
        var versionLabel = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?
            .Split('+')[0]
            .Trim() ?? "1.0.0";

        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UNKCLUB-Tool", versionLabel));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetResponse>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAssetResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
