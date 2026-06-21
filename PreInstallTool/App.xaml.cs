using System.Net;
using System.Net.Http;
using System.Windows;
using PreInstallTool.Localization;
using PreInstallTool.Services;

namespace PreInstallTool;

public partial class App : Application
{
    public static bool ContinueErrorFixRequested { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceService.TryAcquireSingleInstance())
        {
            SingleInstanceService.SignalExistingInstance();
            Shutdown();
            return;
        }

        LocalizationService.Initialize();
        AppResourceService.InvalidateCacheIfAppVersionChanged();

        OsCompatibilityService.EnsureSupportedOrWarn();

        ContinueErrorFixRequested = e.Args.Any(static arg =>
            arg.Equals(PostRebootAutoStartService.ContinueErrorFixArgument, StringComparison.OrdinalIgnoreCase));

        if (ContinueErrorFixRequested)
        {
            PostRebootAutoStartService.WriteAutoStartLog(
                LocalizationService.GetString("AutoStart_StartedWithContinueArg"));
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceService.Dispose();
        base.OnExit(e);
    }

    internal static string MapBundleErrorDetail(Exception ex)
    {
        if (ex.Message is "BUNDLE_URL_UNRESOLVED")
        {
            return LocalizationService.GetString("Bundle_ErrorUrlUnresolved");
        }

        if (ex.Message is "BUNDLE_INVALID_CONTENT")
        {
            return LocalizationService.GetString("Bundle_ErrorInvalidContent");
        }

        if (FindHttpRequestException(ex) is { } networkEx)
        {
            if (networkEx.StatusCode == HttpStatusCode.NotFound)
            {
                return LocalizationService.GetString("Bundle_ErrorNotFoundPrivateRepo");
            }

            return LocalizationService.Format("Bundle_ErrorNetwork", networkEx.Message);
        }

        return string.IsNullOrWhiteSpace(ex.Message) || ex.Message.StartsWith("BUNDLE_", StringComparison.Ordinal)
            ? LocalizationService.GetString("Bundle_ErrorNetworkGeneric")
            : ex.Message;
    }

    private static HttpRequestException? FindHttpRequestException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException httpEx)
            {
                return httpEx;
            }
        }

        return null;
    }
}
