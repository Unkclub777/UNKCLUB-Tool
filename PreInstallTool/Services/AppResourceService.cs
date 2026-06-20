using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace PreInstallTool.Services;

/// <summary>
/// Resolves installer/config paths: beside the exe (legacy/dev), extracted cache under
/// %LocalAppData%\UNKCLUB-Tool\, or downloaded silently from the GitHub Release asset
/// installers-bundle.zip.
/// </summary>
public static class AppResourceService
{
    private const string ExtractedVersionFileName = ".installers-bundle-version";
    private const string LocalAppFolderName = "UNKCLUB-Tool";
    private const int DownloadRetryCount = 3;

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly object InitLock = new();
    private static string? _resourceRoot;
    private static bool _initialized;

    public static event Action<string>? ProgressReported;

    private static void ReportProgress(string message) => ProgressReported?.Invoke(message);

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

    public static void ResetForRetry()
    {
        lock (InitLock)
        {
            _resourceRoot = null;
            _initialized = false;
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
        var requiredBundleVersion = GetRequiredBundleVersion();

        if (Directory.Exists(extractRoot))
        {
            if (IsExtractedBundleValid(extractRoot, versionMarker, requiredBundleVersion))
            {
                return extractRoot;
            }

            ReportProgress(Localization.LocalizationService.Get("Bundle_CacheInvalidRedownload"));
            ClearExtractedCache(extractRoot, versionMarker);
        }

        Directory.CreateDirectory(extractRoot);
        DownloadAndExtractInstallersBundle(extractRoot, requiredBundleVersion);
        File.WriteAllText(versionMarker, requiredBundleVersion);
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
        var ilkKurulumPath = Path.Combine(installersRoot, "IlkKurulum");

        if (!File.Exists(configPath) || !Directory.Exists(installersRoot))
        {
            return false;
        }

        if (!Directory.Exists(ilkKurulumPath))
        {
            return false;
        }

        return Directory
            .EnumerateFiles(ilkKurulumPath, "*.*", SearchOption.AllDirectories)
            .Any(file =>
            {
                var extension = Path.GetExtension(file);
                return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static bool IsExtractedBundleValid(string extractRoot, string versionMarker, string requiredBundleVersion)
    {
        if (!File.Exists(versionMarker) ||
            !File.ReadAllText(versionMarker).Trim().Equals(requiredBundleVersion, StringComparison.Ordinal))
        {
            return false;
        }

        return IsValidResourceRoot(extractRoot);
    }

    private static void ClearExtractedCache(string extractRoot, string versionMarker)
    {
        try
        {
            if (Directory.Exists(extractRoot))
            {
                Directory.Delete(extractRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort; download will overwrite.
        }

        try
        {
            if (File.Exists(versionMarker))
            {
                File.Delete(versionMarker);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void DownloadAndExtractInstallersBundle(string targetRoot, string versionLabel)
    {
        var downloadUrls = ResolveInstallersBundleDownloadUrls(versionLabel);
        if (downloadUrls.Count == 0)
        {
            throw new InvalidOperationException("BUNDLE_URL_UNRESOLVED");
        }

        Exception? lastError = null;
        foreach (var downloadUrl in downloadUrls)
        {
            var tempZip = Path.Combine(
                Path.GetTempPath(),
                $"unkclub-installers-{Guid.NewGuid():N}.zip");

            try
            {
                for (var attempt = 1; attempt <= DownloadRetryCount; attempt++)
                {
                    try
                    {
                        ReportProgress(Localization.LocalizationService.Format(
                            "Bundle_DownloadStarting",
                            UpdateConstants.InstallersBundleFileName));
                        ReportProgress(Localization.LocalizationService.Get("Bundle_Downloading"));

                        DownloadFileAsync(downloadUrl, tempZip).GetAwaiter().GetResult();

                        if (Directory.Exists(targetRoot))
                        {
                            Directory.Delete(targetRoot, recursive: true);
                        }

                        Directory.CreateDirectory(targetRoot);
                        ZipFile.ExtractToDirectory(tempZip, targetRoot, overwriteFiles: true);

                        if (!IsValidResourceRoot(targetRoot))
                        {
                            throw new InvalidOperationException("BUNDLE_INVALID_CONTENT");
                        }

                        return;
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        lastError = ex;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        if (attempt < DownloadRetryCount)
                        {
                            ReportProgress(Localization.LocalizationService.Format(
                                "Bundle_DownloadRetry",
                                attempt + 1,
                                DownloadRetryCount));
                            Thread.Sleep(TimeSpan.FromSeconds(attempt * 2));
                        }
                    }
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

        throw new InvalidOperationException(
            lastError?.Message ?? "BUNDLE_DOWNLOAD_FAILED",
            lastError);
    }

    private static List<string> ResolveInstallersBundleDownloadUrls(string versionLabel)
    {
        var urls = new List<string>();
        var manifest = TryFetchVersionManifest();

        if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.InstallersBundleVersion))
        {
            versionLabel = manifest.InstallersBundleVersion.Trim();
        }

        if (!string.IsNullOrWhiteSpace(manifest?.InstallersBundleUrl))
        {
            AddUniqueUrl(urls, manifest.InstallersBundleUrl);
        }

        AddUniqueUrl(
            urls,
            GitHubReleaseAssetResolver.BuildDirectDownloadUrl(
                versionLabel,
                UpdateConstants.InstallersBundleFileName));

        var apiUrl = GitHubReleaseAssetResolver.ResolveAssetUrl(
            versionLabel,
            UpdateConstants.InstallersBundleFileName);
        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            AddUniqueUrl(urls, apiUrl);
        }

        return urls;
    }

    private static void AddUniqueUrl(List<string> urls, string url)
    {
        if (urls.Any(existing => existing.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        urls.Add(url);
    }

    private static VersionManifestInfo? TryFetchVersionManifest()
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

    private static string GetRequiredBundleVersion()
    {
        var manifest = TryFetchVersionManifest();
        if (!string.IsNullOrWhiteSpace(manifest?.InstallersBundleVersion))
        {
            return manifest.InstallersBundleVersion.Trim();
        }

        return GetCurrentAssemblyVersionLabel();
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException(
                BuildNotFoundDownloadMessage(url),
                inner: null,
                statusCode: HttpStatusCode.NotFound);
        }

        response.EnsureSuccessStatusCode();

        var fileLabel = Path.GetFileName(destinationPath);
        if (string.IsNullOrWhiteSpace(fileLabel))
        {
            fileLabel = UpdateConstants.InstallersBundleFileName;
        }

        var totalBytes = DownloadProgressReporter.TryGetContentLength(response);
        var progress = new Progress<string>(ReportProgress);

        await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await DownloadProgressReporter.CopyWithProgressAsync(
            source,
            destination,
            totalBytes,
            fileLabel,
            progress).ConfigureAwait(false);
    }

    internal static string BuildNotFoundDownloadMessage(string url) =>
        $"HTTP 404 Not Found ({url}). " +
        "The installers bundle may be missing from the release, or the GitHub repository may be private. " +
        "Make the repository public or set a publicly reachable installersBundleUrl in version.json.";

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
        var versionLabel = GetCurrentAssemblyVersionLabel();
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UNKCLUB-Tool", versionLabel));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private sealed class VersionManifestInfo
    {
        public string? InstallersBundleUrl { get; set; }
        public string? InstallersBundleVersion { get; set; }
    }
}
