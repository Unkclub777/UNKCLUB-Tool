using System.Net.Http;
using System.Windows;
using PreInstallTool.Localization;
using PreInstallTool.Services;

namespace PreInstallTool;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\UNKCLUB_PreInstallTool_SingleInstance_v1";
    private static Mutex? _singleInstanceMutex;

    public static bool ContinueErrorFixRequested { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;

            LocalizationService.Initialize();
            MessageBox.Show(
                LocalizationService.GetString("App_AlreadyRunningMessage"),
                LocalizationService.GetString("App_AlreadyRunningTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        LocalizationService.Initialize();

        if (!TryEnsureResourceBundle())
        {
            Shutdown();
            return;
        }

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
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }

    private static bool TryEnsureResourceBundle()
    {
        while (true)
        {
            try
            {
                AppResourceService.EnsureInitialized();
                return true;
            }
            catch (Exception ex)
            {
                var detail = MapBundleErrorDetail(ex);
                var result = MessageBox.Show(
                    LocalizationService.Format("Bundle_DownloadFailed", detail),
                    LocalizationService.GetString("Bundle_PrepareFailedTitle"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Error);

                if (result != MessageBoxResult.OK)
                {
                    return false;
                }

                AppResourceService.ResetForRetry();
            }
        }
    }

    private static string MapBundleErrorDetail(Exception ex)
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
