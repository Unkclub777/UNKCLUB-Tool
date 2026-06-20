namespace PreInstallTool.Services;

/// <summary>
/// Ensures a single app instance; second launches signal the first to activate its window.
/// </summary>
public static class SingleInstanceService
{
    private const string MutexName = "Global\\UNKCLUB_PreInstallTool_SingleInstance_v1";
    private const string ActivateEventName = "Global\\UNKCLUB_PreInstallTool_Activate_v1";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static CancellationTokenSource? _listenerCts;

    public static event Action? ActivationRequested;

    public static bool TryAcquireSingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        StartActivationListener();
        return true;
    }

    public static void SignalExistingInstance()
    {
        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch
        {
            // First instance may still be starting; ignore.
        }
    }

    public static void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _listenerCts = null;

        _activateEvent?.Dispose();
        _activateEvent = null;

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }

    private static void StartActivationListener()
    {
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activateEvent?.WaitOne(500) == true)
                    {
                        ActivationRequested?.Invoke();
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, token);
    }
}
