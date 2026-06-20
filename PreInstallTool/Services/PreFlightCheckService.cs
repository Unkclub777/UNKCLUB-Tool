using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Principal;

namespace PreInstallTool.Services;

public sealed class PreFlightCheckService
{
    private const long MinimumFreeBytes = 500L * 1024 * 1024;
    private const int RequestTimeoutSeconds = 15;

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<PreFlightCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<PreFlightIssue>();
        var isAdmin = IsRunningAsAdministrator();

        if (!isAdmin)
        {
            issues.Add(new PreFlightIssue(
                IsCritical: false,
                MessageKey: "PreFlight_NotAdmin",
                Detail: null));
        }

        if (!await CheckInternetConnectivityAsync(cancellationToken).ConfigureAwait(false))
        {
            issues.Add(new PreFlightIssue(
                IsCritical: true,
                MessageKey: "PreFlight_NoInternet",
                Detail: null));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!HasSufficientDiskSpace(localAppData))
        {
            issues.Add(new PreFlightIssue(
                IsCritical: true,
                MessageKey: "PreFlight_LowDiskSpace",
                Detail: FormatFreeSpace(localAppData)));
        }

        if (await CheckInternetConnectivityAsync(cancellationToken).ConfigureAwait(false))
        {
            var assetIssues = await CheckReleaseAssetsAsync(cancellationToken).ConfigureAwait(false);
            issues.AddRange(assetIssues);
        }

        return new PreFlightCheckResult(
            isAdmin,
            issues,
            issues.All(static issue => !issue.IsCritical));
    }

    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckInternetConnectivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/");
            using var response = await HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode ||
                   response.StatusCode == HttpStatusCode.MethodNotAllowed ||
                   response.StatusCode == HttpStatusCode.Forbidden;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasSufficientDiskSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady && drive.AvailableFreeSpace >= MinimumFreeBytes;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatFreeSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                return string.Empty;
            }

            var freeMb = drive.AvailableFreeSpace / (1024.0 * 1024.0);
            return Localization.LocalizationService.Format("PreFlight_FreeSpaceDetail", freeMb.ToString("F0"));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<List<PreFlightIssue>> CheckReleaseAssetsAsync(CancellationToken cancellationToken)
    {
        var issues = new List<PreFlightIssue>();
        var versionLabel = GetCurrentAssemblyVersionLabel();

        var bundleUrl = GitHubReleaseAssetResolver.BuildDirectDownloadUrl(
            versionLabel,
            UpdateConstants.InstallersBundleFileName);
        if (!await IsAssetReachableAsync(bundleUrl, cancellationToken).ConfigureAwait(false))
        {
            issues.Add(new PreFlightIssue(
                IsCritical: true,
                MessageKey: "PreFlight_BundleUnreachable",
                Detail: UpdateConstants.InstallersBundleFileName));
        }

        var unkclubUrl = GitHubReleaseAssetResolver.BuildDirectDownloadUrl(
            versionLabel,
            UpdateConstants.UnkclubAppFileName);
        if (!await IsAssetReachableAsync(unkclubUrl, cancellationToken).ConfigureAwait(false))
        {
            issues.Add(new PreFlightIssue(
                IsCritical: true,
                MessageKey: "PreFlight_UnkclubUnreachable",
                Detail: UpdateConstants.UnkclubAppFileName));
        }

        return issues;
    }

    private static async Task<bool> IsAssetReachableAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == HttpStatusCode.MethodNotAllowed ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Range = new RangeHeaderValue(0, 0);
                using var getResponse = await HttpClient.SendAsync(
                    getRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token).ConfigureAwait(false);

                return getResponse.IsSuccessStatusCode ||
                       getResponse.StatusCode == HttpStatusCode.PartialContent;
            }

            return false;
        }
        catch
        {
            return false;
        }
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
            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("UNKCLUB-Tool-PreFlight", GetCurrentAssemblyVersionLabel()));
        return client;
    }
}

public sealed record PreFlightIssue(bool IsCritical, string MessageKey, string? Detail);

public sealed record PreFlightCheckResult(
    bool IsAdmin,
    IReadOnlyList<PreFlightIssue> Issues,
    bool CanProceed);
