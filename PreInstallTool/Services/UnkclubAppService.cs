using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

/// <summary>
/// Downloads UNKCLUB.exe from GitHub releases and deploys it to the desktop unkclub(new) folder.
/// </summary>
public static class UnkclubAppService
{
    private const int DownloadRetryCount = 3;
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static string DefaultDesktopPath =>
        Path.Combine(
            DesktopPathService.GetEmulatorFolderPath(),
            UpdateConstants.UnkclubAppFileName);

    public static async Task<string> DownloadToPathAsync(
        string destinationPath,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        var versionLabel = GetVersionLabel();
        var downloadUrls = ResolveDownloadUrls(versionLabel);
        if (downloadUrls.Count == 0)
        {
            throw new InvalidOperationException("UNKCLUB_DOWNLOAD_URL_UNRESOLVED");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"unkclub-app-{Guid.NewGuid():N}.exe");

        Exception? lastError = null;
        try
        {
            foreach (var downloadUrl in downloadUrls)
            {
                for (var attempt = 1; attempt <= DownloadRetryCount; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        log?.Report(Localization.LocalizationService.Format(
                            "UnkclubApp_Downloading",
                            Path.GetFileName(destinationPath)));

                        await DownloadFileAsync(downloadUrl, tempPath, log, cancellationToken).ConfigureAwait(false);

                        if (!File.Exists(tempPath) || new FileInfo(tempPath).Length < 1024)
                        {
                            throw new InvalidOperationException("UNKCLUB_DOWNLOAD_INVALID");
                        }

                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }

                        File.Move(tempPath, destinationPath);
                        SystemChecks.UnblockFile(destinationPath);

                        log?.Report(Localization.LocalizationService.Format(
                            "UnkclubApp_Downloaded",
                            destinationPath));

                        return destinationPath;
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        lastError = ex;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        log?.Report(Localization.LocalizationService.Format(
                            "UnkclubApp_RetryAttempt",
                            attempt,
                            DownloadRetryCount,
                            ex.Message));

                        if (attempt < DownloadRetryCount)
                        {
                            log?.Report(Localization.LocalizationService.Format(
                                "UnkclubApp_DeployRetry",
                                attempt + 1,
                                DownloadRetryCount));

                            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        throw new InvalidOperationException(
            lastError?.Message ?? "UNKCLUB_DOWNLOAD_FAILED",
            lastError);
    }

    public static Task<string> DeployToDesktopAsync(
        IProgress<string>? log,
        CancellationToken cancellationToken) =>
        DownloadToPathAsync(DefaultDesktopPath, log, cancellationToken);

    public static bool IsGitHubDeployStep(InstallStep step)
    {
        if (step.DownloadFromGitHub)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(step.SourcePath))
        {
            return false;
        }

        return step.SourcePath.Contains("UNKCLUB", StringComparison.OrdinalIgnoreCase) ||
               step.SourcePath.StartsWith("github:", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveDestinationPath(InstallStep step)
    {
        const string defaultFolderName = UpdateConstants.DesktopDeployFolderName;
        const string defaultFileName = UpdateConstants.UnkclubAppFileName;

        if (!string.IsNullOrWhiteSpace(step.DestinationPath))
        {
            return SystemChecks.ExpandPath(step.DestinationPath);
        }

        if (!string.IsNullOrWhiteSpace(step.DestinationFolder) &&
            !string.IsNullOrWhiteSpace(step.FileName))
        {
            var destinationFolder = SystemChecks.ExpandPath(step.DestinationFolder);
            if (!string.IsNullOrWhiteSpace(step.DuplicateFolderStrategy))
            {
                destinationFolder = DesktopPathService.ResolveUniqueDestinationFolder(
                    destinationFolder,
                    step.DuplicateFolderStrategy);
            }

            return Path.Combine(destinationFolder, step.FileName);
        }

        var folderName = string.IsNullOrWhiteSpace(step.DesktopFolderName)
            ? defaultFolderName
            : step.DesktopFolderName;

        return Path.Combine(
            DesktopPathService.GetEmulatorFolderPath(folderName),
            defaultFileName);
    }

    private static List<string> ResolveDownloadUrls(string versionLabel)
    {
        var urls = new List<string>();
        var manifest = TryFetchVersionManifest();

        if (!string.IsNullOrWhiteSpace(manifest?.UnkclubAppUrl))
        {
            AddUniqueUrl(urls, manifest.UnkclubAppUrl);
        }

        AddUniqueUrl(
            urls,
            GitHubReleaseAssetResolver.BuildDirectDownloadUrl(
                versionLabel,
                UpdateConstants.UnkclubAppFileName));

        var apiUrl = GitHubReleaseAssetResolver.ResolveAssetUrl(
            versionLabel,
            UpdateConstants.UnkclubAppFileName);
        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            AddUniqueUrl(urls, apiUrl);
        }

        return urls;
    }

    private static VersionManifestInfo? TryFetchVersionManifest()
    {
        var manifestUrl =
            $"https://raw.githubusercontent.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/{UpdateConstants.DefaultBranch}/version.json";

        try
        {
            var manifestJson = HttpClient.GetStringAsync(manifestUrl).GetAwaiter().GetResult();
            return System.Text.Json.JsonSerializer.Deserialize<VersionManifestInfo>(manifestJson);
        }
        catch
        {
            return null;
        }
    }

    private static void AddUniqueUrl(List<string> urls, string url)
    {
        if (urls.Any(existing => existing.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        urls.Add(url);
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException(
                AppResourceService.BuildNotFoundDownloadMessage(url),
                inner: null,
                statusCode: HttpStatusCode.NotFound);
        }

        response.EnsureSuccessStatusCode();

        var fileLabel = Path.GetFileName(destinationPath);
        if (string.IsNullOrWhiteSpace(fileLabel))
        {
            fileLabel = UpdateConstants.UnkclubAppFileName;
        }

        var totalBytes = DownloadProgressReporter.TryGetContentLength(response);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await DownloadProgressReporter.CopyWithProgressAsync(
            source,
            destination,
            totalBytes,
            fileLabel,
            log,
            cancellationToken).ConfigureAwait(false);
    }

    private static string GetVersionLabel()
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
        var versionLabel = GetVersionLabel();
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UNKCLUB-Tool", versionLabel));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }

    private sealed class VersionManifestInfo
    {
        public string? UnkclubAppUrl { get; set; }
    }
}
