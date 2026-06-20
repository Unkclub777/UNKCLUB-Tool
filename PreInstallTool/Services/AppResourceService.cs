using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace PreInstallTool.Services;

/// <summary>
/// Resolves installer/config paths: beside the exe (legacy/dev), extracted cache under
/// %LocalAppData%\UNKCLUB-Tool\, or downloaded once from the GitHub Release asset
/// installers-bundle.zip.
/// </summary>
public static class AppResourceService
{
    private const string ExtractedVersionFileName = ".installers-bundle-version";
    private const string LocalAppFolderName = "UNKCLUB-Tool";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly object InitLock = new();
    private static string? _resourceRoot;
    private static bool _initialized;

    public static string ResourceRoot
    {
        get
        {
            EnsureInitialized();
            return _resourceRoot!;
        }
    }

    public static string GetPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(ResourceRoot, normalized));
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            _resourceRoot = ResolveResourceRoot();
            _initialized = true;
        }
    }

    private static string ResolveResourceRoot()
    {
        var devRoot = TryResolveDevResourceRoot();
        if (devRoot is not null)
        {
            return devRoot;
        }

        var extractRoot = GetLocalCacheRoot();
        var versionMarker = Path.Combine(extractRoot, ExtractedVersionFileName);
        var currentVersion = GetCurrentAssemblyVersionLabel();

        if (IsExtractedBundleValid(extractRoot, versionMarker, currentVersion))
        {
            return extractRoot;
        }

        Directory.CreateDirectory(extractRoot);
        DownloadAndExtractInstallersBundle(extractRoot, currentVersion);
        File.WriteAllText(versionMarker, currentVersion);
        return extractRoot;
    }

    private static string GetLocalCacheRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LocalAppFolderName);

    private static string? TryResolveDevResourceRoot()
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        if (IsValidResourceRoot(baseDirectory))
        {
            return baseDirectory;
        }

        var directory = baseDirectory;
        for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(directory); depth++)
        {
            var preInstallToolDir = Path.Combine(directory, "PreInstallTool");
            var stagingRoot = Path.Combine(preInstallToolDir, "embedded-staging");
            if (IsValidResourceRoot(stagingRoot))
            {
                return stagingRoot;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static bool IsValidResourceRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return false;
        }

        var installersRoot = Path.Combine(root, "Installers");
        var configPath = Path.Combine(root, "install-config.json");

        return File.Exists(configPath) &&
               Directory.Exists(installersRoot) &&
               Directory.EnumerateFileSystemEntries(installersRoot).Any();
    }

    private static bool IsExtractedBundleValid(string extractRoot, string versionMarker, string currentVersion)
    {
        if (!File.Exists(versionMarker) ||
            !File.ReadAllText(versionMarker).Trim().Equals(currentVersion, StringComparison.Ordinal))
        {
            return false;
        }

        return IsValidResourceRoot(extractRoot);
    }

    private static void DownloadAndExtractInstallersBundle(string targetRoot, string versionLabel)
    {
        var downloadUrl = ResolveInstallersBundleDownloadUrl(versionLabel)
            ?? throw new InvalidOperationException(
                $"Could not resolve a download URL for {UpdateConstants.InstallersBundleFileName} (v{versionLabel}).");

        var tempZip = Path.Combine(
            Path.GetTempPath(),
            $"unkclub-installers-{Guid.NewGuid():N}.zip");

        try
        {
            DownloadFileAsync(downloadUrl, tempZip).GetAwaiter().GetResult();

            if (Directory.Exists(targetRoot))
            {
                Directory.Delete(targetRoot, recursive: true);
            }

            Directory.CreateDirectory(targetRoot);
            ZipFile.ExtractToDirectory(tempZip, targetRoot, overwriteFiles: true);

            if (!IsValidResourceRoot(targetRoot))
            {
                throw new InvalidOperationException(
                    $"Downloaded {UpdateConstants.InstallersBundleFileName} does not contain install-config.json and Installers.");
            }
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }
    }

    private static string? ResolveInstallersBundleDownloadUrl(string versionLabel)
    {
        var manifestUrl =
            $"https://raw.githubusercontent.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/{UpdateConstants.DefaultBranch}/version.json";

        try
        {
            var manifestJson = HttpClient.GetStringAsync(manifestUrl).GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(manifestJson);
            var root = document.RootElement;

            if (root.TryGetProperty("installersBundleUrl", out var bundleUrlElement))
            {
                var bundleUrl = bundleUrlElement.GetString();
                if (!string.IsNullOrWhiteSpace(bundleUrl))
                {
                    return bundleUrl;
                }
            }

            if (root.TryGetProperty("installersBundleVersion", out var bundleVersionElement))
            {
                versionLabel = bundleVersionElement.GetString() ?? versionLabel;
            }
        }
        catch
        {
            // Fall back to the release asset URL derived from the app version.
        }

        return BuildReleaseAssetUrl(versionLabel, UpdateConstants.InstallersBundleFileName);
    }

    internal static string BuildReleaseAssetUrl(string versionLabel, string assetFileName)
    {
        var normalized = versionLabel.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return $"https://github.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/download/v{normalized}/{assetFileName}";
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination).ConfigureAwait(false);
    }

    private static string GetCurrentAssemblyVersionLabel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+')[0].Trim();
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UNKCLUB-Tool", "1.2.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
