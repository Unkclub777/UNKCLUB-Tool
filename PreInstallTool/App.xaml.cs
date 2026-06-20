using System.Windows;
using PreInstallTool.Localization;
using PreInstallTool.Services;

namespace PreInstallTool;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\UNKCLUB_PreInstallTool_SingleInstance_v1";
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsSingleInstanceMutex;

    public static bool ContinueErrorFixRequested { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _ownsSingleInstanceMutex = true;

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
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
