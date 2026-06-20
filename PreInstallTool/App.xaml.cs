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

        AppResourceService.EnsureInitialized();
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
}
