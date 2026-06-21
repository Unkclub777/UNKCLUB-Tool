using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class AutoUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static Version CurrentVersion => GetCurrentVersion();

    public static string CurrentVersionLabel =>
        CurrentVersion.ToString(3);

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (UpdateCheckSkipCache.ShouldSkip())
        {
            UpdateDiagnostics.LogWarning("Skipping update check due to recent 404 (cached 24h).");
            return new UpdateCheckResult(
                UpdateStatus.CheckFailed,
                ErrorMessage: "Update check skipped after recent download failure.",
                IsNotFoundFailure: true);
        }

        var releaseResult = await TryCheckGitHubReleaseAsync(cancellationToken).ConfigureAwait(false);

        if (releaseResult.Status == UpdateStatus.UpToDate)
        {
            return releaseResult;
        }

        if (releaseResult.Status == UpdateStatus.UpdateAvailable)
        {
            return releaseResult;
        }

        if (releaseResult.Status == UpdateStatus.CheckFailed && releaseResult.IsNotFoundFailure)
        {
            UpdateCheckSkipCache.RecordNotFound();
            return releaseResult;
        }

        return await TryCheckVersionJsonAsync(releaseResult.RemoteVersion, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<UpdateApplyResult> DownloadAndApplyUpdateAsync(
        UpdateCheckResult update,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (update.Status != UpdateStatus.UpdateAvailable ||
            update.RemoteVersion is null ||
            string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            return new UpdateApplyResult(false, "No update package is available.");
        }

        try
        {
            if (!await GitHubReleaseAssetResolver
                    .IsUrlReachableAsync(update.DownloadUrl, cancellationToken)
                    .ConfigureAwait(false))
            {
                UpdateCheckSkipCache.RecordNotFound();
                UpdateDiagnostics.LogWarning($"Update download URL not reachable: {update.DownloadUrl}");
                return new UpdateApplyResult(
                    false,
                    "Release download URL returned HTTP 404.",
                    IsNotFoundFailure: true);
            }

            var executablePath = GetExecutablePath();
            var installDirectory = Path.GetDirectoryName(executablePath)
                ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var updatesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                UpdateConstants.LocalAppFolderName,
                "Updates");
            Directory.CreateDirectory(updatesRoot);

            var versionFolder = Path.Combine(updatesRoot, update.RemoteVersion.ToString(3));
            if (Directory.Exists(versionFolder))
            {
                Directory.Delete(versionFolder, recursive: true);
            }

            Directory.CreateDirectory(versionFolder);

            var stagingDirectory = Path.Combine(versionFolder, "staging");
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }

            Directory.CreateDirectory(stagingDirectory);
            progress?.Report(update.DownloadUrl);

            string payloadExe;
            if (IsZipDownload(update.DownloadUrl))
            {
                var zipPath = Path.Combine(versionFolder, UpdateConstants.ReleaseAssetFileName);
                await DownloadFileAsync(update.DownloadUrl, zipPath, progress, cancellationToken).ConfigureAwait(false);
                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, overwriteFiles: true);
                payloadExe = ResolvePayloadExecutable(stagingDirectory);
            }
            else
            {
                payloadExe = Path.Combine(stagingDirectory, UpdateConstants.ReleaseExecutableFileName);
                await DownloadFileAsync(update.DownloadUrl, payloadExe, progress, cancellationToken).ConfigureAwait(false);
            }

            var updaterScript = Path.Combine(versionFolder, "apply-update.cmd");
            WriteUpdaterScript(updaterScript, payloadExe, executablePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterScript,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return new UpdateApplyResult(true);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            UpdateCheckSkipCache.RecordNotFound();
            UpdateDiagnostics.LogWarning($"Update download failed with 404: {ex.Message}");
            return new UpdateApplyResult(false, ex.Message, IsNotFoundFailure: true);
        }
        catch (Exception ex)
        {
            return new UpdateApplyResult(false, ex.Message);
        }
    }

    private static async Task<UpdateCheckResult> TryCheckGitHubReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"https://api.github.com/repos/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/latest";
            using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                UpdateDiagnostics.LogWarning($"GitHub releases/latest returned {(int)response.StatusCode}.");
                return new UpdateCheckResult(
                    UpdateStatus.CheckFailed,
                    ErrorMessage: $"GitHub API: {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (release is null || !TryParseVersion(release.TagName, out var remoteVersion))
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: "Invalid release metadata.");
            }

            if (CurrentVersion >= remoteVersion)
            {
                return new UpdateCheckResult(UpdateStatus.UpToDate, RemoteVersion: remoteVersion);
            }

            var apiAssets = release.Assets?
                .Select(a => new GitHubReleaseAssetInfo(a.Name, a.BrowserDownloadUrl))
                .ToList();

            var downloadUrl = await GitHubReleaseAssetResolver
                .ResolveVerifiedReleaseExecutableUrlAsync(
                    remoteVersion.ToString(3),
                    apiAssets,
                    cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                UpdateDiagnostics.LogWarning(
                    $"Release v{remoteVersion} has no reachable executable asset (checked direct URLs and API).");
                return new UpdateCheckResult(
                    UpdateStatus.CheckFailed,
                    RemoteVersion: remoteVersion,
                    ErrorMessage: "Release asset not found or unreachable.",
                    IsNotFoundFailure: true);
            }

            return new UpdateCheckResult(
                UpdateStatus.UpdateAvailable,
                remoteVersion,
                downloadUrl,
                release.Body);
        }
        catch (Exception ex)
        {
            UpdateDiagnostics.LogWarning($"GitHub release check failed: {ex.Message}");
            return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: ex.Message);
        }
    }

    private static async Task<UpdateCheckResult> TryCheckVersionJsonAsync(
        Version? latestReleaseVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"https://raw.githubusercontent.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/{UpdateConstants.DefaultBranch}/version.json";
            using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    UpdateStatus.CheckFailed,
                    ErrorMessage: $"version.json: {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<VersionManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (manifest is null || !TryParseVersion(manifest.Version, out var remoteVersion))
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: "Invalid version.json.");
            }

            if (latestReleaseVersion is not null && remoteVersion > latestReleaseVersion)
            {
                UpdateDiagnostics.LogWarning(
                    $"version.json ({remoteVersion}) is newer than latest GitHub release ({latestReleaseVersion}); ignoring manifest version.");
                remoteVersion = latestReleaseVersion;
            }

            if (CurrentVersion >= remoteVersion)
            {
                return new UpdateCheckResult(UpdateStatus.UpToDate, RemoteVersion: remoteVersion);
            }

            var downloadUrl = await ResolveVerifiedManifestDownloadUrlAsync(
                    manifest,
                    remoteVersion.ToString(3),
                    cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                UpdateDiagnostics.LogWarning(
                    $"version.json advertises v{remoteVersion} but no reachable download URL was found.");
                return new UpdateCheckResult(
                    UpdateStatus.CheckFailed,
                    RemoteVersion: remoteVersion,
                    ErrorMessage: "Update download URL unreachable.",
                    IsNotFoundFailure: true);
            }

            return new UpdateCheckResult(
                UpdateStatus.UpdateAvailable,
                remoteVersion,
                downloadUrl,
                manifest.ReleaseNotes);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: ex.Message);
        }
    }

    private static async Task<string?> ResolveVerifiedManifestDownloadUrlAsync(
        VersionManifest manifest,
        string versionLabel,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(manifest.DownloadUrl) &&
            await GitHubReleaseAssetResolver
                .IsUrlReachableAsync(manifest.DownloadUrl, cancellationToken)
                .ConfigureAwait(false))
        {
            return manifest.DownloadUrl;
        }

        return await GitHubReleaseAssetResolver
            .ResolveVerifiedReleaseExecutableUrlAsync(versionLabel, apiAssets: null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsZipDownload(string downloadUrl) =>
        downloadUrl.Contains(UpdateConstants.ReleaseAssetFileName, StringComparison.OrdinalIgnoreCase) ||
        downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var fileLabel = Path.GetFileName(destinationPath);
        if (string.IsNullOrWhiteSpace(fileLabel))
        {
            fileLabel = UpdateConstants.ReleaseExecutableFileName;
        }

        var totalBytes = DownloadProgressReporter.TryGetContentLength(response);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await DownloadProgressReporter.CopyWithProgressAsync(
            source,
            destination,
            totalBytes,
            fileLabel,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private static string ResolvePayloadExecutable(string stagingDirectory)
    {
        foreach (var name in UpdateConstants.ReleaseExecutableFileNames)
        {
            var directPath = Path.Combine(stagingDirectory, name);
            if (File.Exists(directPath))
            {
                return directPath;
            }
        }

        var nestedMatch = Directory
            .EnumerateFiles(stagingDirectory, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(path =>
                UpdateConstants.ReleaseExecutableFileNames.Any(name =>
                    path.EndsWith(name, StringComparison.OrdinalIgnoreCase)));

        if (nestedMatch is not null)
        {
            return nestedMatch;
        }

        var anyExe = Directory.EnumerateFiles(stagingDirectory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (anyExe is null)
        {
            throw new FileNotFoundException("Update package does not contain an executable.");
        }

        return anyExe;
    }

    private static void WriteUpdaterScript(
        string scriptPath,
        string payloadExecutable,
        string targetExecutable)
    {
        var continueArg = ErrorFixStateService.IsPostRebootContinuation()
            ? PostRebootAutoStartService.ContinueErrorFixArgument
            : string.Empty;
        var launchCommand = string.IsNullOrWhiteSpace(continueArg)
            ? $"start \"\" \"{targetExecutable.Replace("\"", "\"\"")}\""
            : $"start \"\" \"{targetExecutable.Replace("\"", "\"\"")}\" {continueArg}";

        var script = $"""
            @echo off
            setlocal
            timeout /t 2 /nobreak >nul
            taskkill /F /IM "UNKCLUB Tool.exe" >nul 2>&1
            taskkill /F /IM PreInstallTool.exe >nul 2>&1
            timeout /t 1 /nobreak >nul
            copy /Y "{payloadExecutable.Replace("\"", "\"\"")}" "{targetExecutable.Replace("\"", "\"\"")}" >nul
            {launchCommand}
            endlocal
            del "%~f0"
            """;

        File.WriteAllText(scriptPath, script);
    }

    private static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational) &&
            Version.TryParse(informational.Split('+')[0], out var parsedInformational))
        {
            return parsedInformational;
        }

        return assembly.GetName().Version ?? new Version(1, 0, 0);
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        return Version.TryParse(normalized, out version!);
    }

    public static string GetExecutablePath() =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? Path.Combine(AppContext.BaseDirectory, "PreInstallTool.exe");

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UNKCLUB-Tool-Updater", CurrentVersionLabel));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }

    private sealed class VersionManifest
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }
    }
}
