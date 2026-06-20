using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        var releaseResult = await TryCheckGitHubReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (releaseResult.Status != UpdateStatus.CheckFailed)
        {
            return releaseResult;
        }

        return await TryCheckVersionJsonAsync(cancellationToken).ConfigureAwait(false);
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
            var executablePath = GetExecutablePath();
            var installDirectory = Path.GetDirectoryName(executablePath)
                ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var updatesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UNKCLUB-Tool",
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
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: $"GitHub API: {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (release is null || !TryParseVersion(release.TagName, out var remoteVersion))
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: "Invalid release metadata.");
            }

            var downloadUrl = FindReleaseAssetUrl(release.Assets);

            if (CurrentVersion >= remoteVersion)
            {
                return new UpdateCheckResult(UpdateStatus.UpToDate, RemoteVersion: remoteVersion);
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: "Release asset not found.");
            }

            return new UpdateCheckResult(
                UpdateStatus.UpdateAvailable,
                remoteVersion,
                downloadUrl,
                release.Body);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: ex.Message);
        }
    }

    private static async Task<UpdateCheckResult> TryCheckVersionJsonAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"https://raw.githubusercontent.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/{UpdateConstants.DefaultBranch}/version.json";
            using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: $"version.json: {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<VersionManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (manifest is null || !TryParseVersion(manifest.Version, out var remoteVersion))
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: "Invalid version.json.");
            }

            if (CurrentVersion >= remoteVersion)
            {
                return new UpdateCheckResult(UpdateStatus.UpToDate, RemoteVersion: remoteVersion);
            }

            if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: "version.json has no downloadUrl.");
            }

            return new UpdateCheckResult(
                UpdateStatus.UpdateAvailable,
                remoteVersion,
                manifest.DownloadUrl,
                manifest.ReleaseNotes);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateStatus.CheckFailed, ErrorMessage: ex.Message);
        }
    }

    private static string? FindReleaseAssetUrl(List<GitHubReleaseAsset>? assets)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        return assets
            .FirstOrDefault(a => a.Name.Equals(UpdateConstants.ReleaseExecutableFileName, StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;
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
        var preferredNames = new[]
        {
            "UNKCLUB Tool.exe",
            "PreInstallTool.exe"
        };

        foreach (var name in preferredNames)
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
                path.EndsWith("UNKCLUB Tool.exe", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("PreInstallTool.exe", StringComparison.OrdinalIgnoreCase));

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
