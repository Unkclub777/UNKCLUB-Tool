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
            Shutdown();
            return;
        }

        LocalizationService.Initialize();

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
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
