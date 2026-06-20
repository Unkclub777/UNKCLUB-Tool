using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PreInstallTool.Localization;
using PreInstallTool.Models;
using PreInstallTool.Services;

namespace PreInstallTool.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly InstallOrchestrator _orchestrator = new();
    private CancellationTokenSource? _cancellationTokenSource;

    private string _appTitle = string.Empty;
    private string _description = string.Empty;
    private string _selectedModeDescription = string.Empty;
    private string _statusText = string.Empty;
    private double _progressValue;
    private bool _isRunning;
    private bool _isCompleted;
    private bool _hasFailed;
    private InstallConfig? _config;
    private InstallMode? _selectedMode;
    private LanguageOption? _selectedLanguage;
    private ResellerType _selectedReseller = ResellerSelectionService.Current;
    private readonly List<StartupLogEntry> _startupLogEntries = [];
    private bool _postRebootResumeAttempted;
    private bool _isCheckingForUpdates;

    public MainViewModel()
    {
        DefenderStatus = new DefenderStatusViewModel();
        Modes = new ObservableCollection<ModeOptionViewModel>();
        Resellers = new ObservableCollection<ResellerOptionViewModel>();
        Steps = new ObservableCollection<StepItemViewModel>();
        LogLines = new ObservableCollection<string>();
        Languages = new ObservableCollection<LanguageOption>(LocalizationService.AvailableLanguages);

        SelectModeCommand = new RelayCommand<string>(SelectMode, _ => !IsRunning);
        SelectResellerCommand = new RelayCommand<string>(SelectReseller, _ => !IsRunning);
        StartCommand = new RelayCommand(async () => await StartInstallAsync(), () => !IsRunning && SelectedMode is not null);
        CancelCommand = new RelayCommand(CancelInstall, () => IsRunning);
        LaunchMainProgramCommand = new RelayCommand(LaunchMainProgram, () => IsCompleted && !HasFailed);
        CheckForUpdatesCommand = new RelayCommand(
            async () => await CheckForUpdatesAsync(silent: false),
            () => !IsRunning && !IsCheckingForUpdates);

        _selectedLanguage = Languages.FirstOrDefault(l =>
            l.CultureName.Equals(LocalizationService.CurrentCultureName, StringComparison.OrdinalIgnoreCase))
            ?? Languages[0];

        LocalizationService.LanguageChanged += (_, _) => ApplyLocalization();
        ApplyLocalization();
        InitializeResellers();
        LoadConfig();
    }

    public DefenderStatusViewModel DefenderStatus { get; }

    public ObservableCollection<ModeOptionViewModel> Modes { get; }
    public ObservableCollection<ResellerOptionViewModel> Resellers { get; }
    public ObservableCollection<StepItemViewModel> Steps { get; }
    public ObservableCollection<string> LogLines { get; }
    public ObservableCollection<LanguageOption> Languages { get; }

    public ICommand SelectModeCommand { get; }
    public ICommand SelectResellerCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand LaunchMainProgramCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    public string AppVersionLabel =>
        LocalizationService.Format("AppVersionLabel", AutoUpdateService.CurrentVersionLabel);

    public string BtnCheckForUpdates => LocalizationService.Get("BtnCheckForUpdates");

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (SetProperty(ref _isCheckingForUpdates, value))
            {
                ((RelayCommand)CheckForUpdatesCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            LocalizationService.SetLanguage(value.CultureName);
        }
    }

    public string WindowTitle => LocalizationService.Get("WindowTitle");
    public string LanguageLabel => LocalizationService.Get("LanguageLabel");
    public string OptionLabel => LocalizationService.Get("OptionLabel");
    public string ResellerLabel => LocalizationService.Get("ResellerLabel");
    public string AppTagline => LocalizationService.Get("AppTagline");
    public string LogLabel => LocalizationService.Get("LogLabel");
    public string BtnStartInstall => LocalizationService.Get("BtnStartInstall");
    public string BtnCancel => LocalizationService.Get("BtnCancel");
    public string BtnLaunchMainProgram => LocalizationService.Get("BtnLaunchMainProgram");

    public InstallMode? SelectedMode
    {
        get => _selectedMode;
        private set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                SelectedModeDescription = value is null
                    ? LocalizationService.Get("PromptSelectOption")
                    : GetLocalizedModeDescription(value);
                LoadStepsForSelectedMode();
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ResellerType SelectedReseller
    {
        get => _selectedReseller;
        private set
        {
            if (SetProperty(ref _selectedReseller, value))
            {
                ResellerSelectionService.Current = value;
                LoadStepsForSelectedMode();
            }
        }
    }

    public string AppTitle
    {
        get => _appTitle;
        private set => SetProperty(ref _appTitle, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string SelectedModeDescription
    {
        get => _selectedModeDescription;
        private set => SetProperty(ref _selectedModeDescription, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
                ((RelayCommand<string>)SelectModeCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                ((RelayCommand)LaunchMainProgramCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasFailed
    {
        get => _hasFailed;
        private set
        {
            if (SetProperty(ref _hasFailed, value))
            {
                ((RelayCommand)LaunchMainProgramCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanLaunchMainProgram =>
        _config?.LaunchMainProgramWhenDone == true &&
        !string.IsNullOrWhiteSpace(_config.MainProgramPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void LoadConfig()
    {
        try
        {
            _config = ConfigLoader.Load();
            AppTitle = LocalizationService.Get("AppName_Localized");
            Description = LocalizationService.Get("AppDescription_Localized");
            OnPropertyChanged(nameof(CanLaunchMainProgram));

            Modes.Clear();
            foreach (var mode in _config.Modes)
            {
                Modes.Add(new ModeOptionViewModel(mode));
            }

            if (Modes.Count == 0 && _config.Steps.Count > 0)
            {
                var legacyMode = new InstallMode
                {
                    Id = "default",
                    Name = LocalizationService.Get("LegacyModeName"),
                    Description = _config.Description,
                    Steps = _config.Steps
                };
                Modes.Add(new ModeOptionViewModel(legacyMode));
            }

            AddStartupLog(StartupLogType.ConfigLoaded);

            if (Modes.Count > 0)
            {
                var selectedId = SelectedMode?.Id ?? Modes[0].Id;
                SelectMode(selectedId);
            }
        }
        catch (Exception ex)
        {
            StatusText = LocalizationService.Get("ConfigLoadFailed");
            AddStartupLog(StartupLogType.Error, errorMessage: ex.Message);
            MessageBox.Show(
                ex.Message,
                LocalizationService.GetString("ConfigErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SelectMode(string? modeId)
    {
        if (string.IsNullOrWhiteSpace(modeId))
        {
            return;
        }

        var mode = Modes.FirstOrDefault(m => m.Id == modeId);
        if (mode is null)
        {
            return;
        }

        foreach (var item in Modes)
        {
            item.IsSelected = item.Id == modeId;
        }

        SelectedMode = mode.Mode;
        var localizedName = GetLocalizedModeName(mode.Mode);
        StatusText = LocalizationService.Format("ModeSelectedStatus", localizedName);
        AddStartupLog(StartupLogType.ModeSelected, modeId: modeId);
    }

    private void InitializeResellers()
    {
        Resellers.Clear();
        Resellers.Add(new ResellerOptionViewModel(ResellerType.Unkclub));
        Resellers.Add(new ResellerOptionViewModel(ResellerType.OtherReseller));
        SelectReseller(_selectedReseller.ToString());
    }

    private void SelectReseller(string? resellerId)
    {
        if (string.IsNullOrWhiteSpace(resellerId))
        {
            return;
        }

        if (!Enum.TryParse<ResellerType>(resellerId, ignoreCase: true, out var reseller))
        {
            return;
        }

        foreach (var item in Resellers)
        {
            item.IsSelected = item.ResellerType == reseller;
        }

        SelectedReseller = reseller;

        if (!IsRunning && !IsCompleted && SelectedMode is not null)
        {
            StatusText = LocalizationService.Format(
                "ResellerSelectedStatus",
                GetLocalizedResellerName(reseller),
                GetLocalizedModeName(SelectedMode));
        }
    }

    private void LoadStepsForSelectedMode()
    {
        Steps.Clear();

        if (SelectedMode is null)
        {
            return;
        }

        foreach (var step in BuildEffectiveSteps(SelectedMode))
        {
            Steps.Add(new StepItemViewModel(step));
        }

        if ((SelectedMode.Id.Equals("error-fix", StringComparison.OrdinalIgnoreCase) ||
             SelectedMode.Id.Equals("first-install", StringComparison.OrdinalIgnoreCase)) &&
            ErrorFixStateService.IsPostRebootContinuation())
        {
            StatusText = LocalizationService.Get("ErrorFix_PostRebootResuming");
        }
    }

    private static IEnumerable<InstallStep> GetBaseVisibleSteps(InstallMode mode)
    {
        if (!mode.Id.Equals("error-fix", StringComparison.OrdinalIgnoreCase) &&
            !mode.Id.Equals("first-install", StringComparison.OrdinalIgnoreCase))
        {
            return mode.Steps.Where(static s => s.Enabled);
        }

        var phase = ErrorFixStateService.GetPhase();
        return mode.Steps
            .Where(s => s.Enabled && ErrorFixStateService.ShouldRunStep(s.Phase, phase));
    }

    private static IReadOnlyList<InstallStep> BuildEffectiveSteps(InstallMode mode) =>
        ResellerStepBuilder.ApplyResellerSteps(GetBaseVisibleSteps(mode).ToList(), mode);

    private IReadOnlyList<InstallStep> GetStepsForExecution()
    {
        if (SelectedMode is null)
        {
            return [];
        }

        return BuildEffectiveSteps(SelectedMode);
    }

    private async Task StartInstallAsync()
    {
        if (_config is null || SelectedMode is null)
        {
            return;
        }

        IsRunning = true;
        IsCompleted = false;
        HasFailed = false;
        ProgressValue = 0;
        LogLines.Clear();
        _startupLogEntries.Clear();

        foreach (var step in Steps)
        {
            step.Reset();
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var modeName = GetLocalizedModeName(SelectedMode);
        InstallSessionService.CurrentModeId = SelectedMode.Id;
        AddLog(LocalizationService.Format("InstallStarted", modeName));

        try
        {
            var progress = new Progress<(int current, int total, StepProgress step)>(report =>
            {
                ProgressValue = report.total == 0
                    ? 0
                    : (double)report.current / report.total * 100;

                var stepName = LocalizationService.GetLocalizedStepName(
                    report.step.Step.Id,
                    report.step.Step.Name);

                StatusText = LocalizationService.Format(
                    "ProgressFormat",
                    report.current,
                    report.total,
                    stepName,
                    report.step.Message);

                var vm = Steps.FirstOrDefault(s => s.Id == report.step.Step.Id);
                vm?.Update(report.step);

                if (IsDefenderDisableStep(report.step.Step.Id) &&
                    report.step.Status is StepStatus.Success or StepStatus.Skipped)
                {
                    DefenderStatus.Refresh();
                }
            });

            var log = new Progress<string>(AddLog);

            var results = await _orchestrator.RunAsync(
                GetStepsForExecution(),
                progress,
                log,
                _cancellationTokenSource.Token,
                () => DefenderStatus.Refresh());

            HasFailed = results.Any(r =>
                r.Status == StepStatus.Failed && !r.Step.Optional);

            IsCompleted = true;
            StatusText = HasFailed
                ? LocalizationService.Format("InstallCompletedErrors", modeName)
                : LocalizationService.Format("InstallCompletedSuccess", modeName);

            AddLog(StatusText);

            if (!HasFailed && _config.LaunchMainProgramWhenDone)
            {
                LaunchMainProgram();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService.Get("InstallCancelled");
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            HasFailed = true;
            IsCompleted = true;
            StatusText = LocalizationService.Get("UnexpectedError");
            AddLog($"{LocalizationService.Get("ErrorPrefix")} {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            InstallSessionService.CurrentModeId = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            DefenderStatus.Refresh();
        }
    }

    private void CancelInstall()
    {
        _cancellationTokenSource?.Cancel();
    }

    private void LaunchMainProgram()
    {
        if (_config is null)
        {
            return;
        }

        try
        {
            _orchestrator.LaunchMainProgram(_config);
            AddLog(LocalizationService.Get("MainProgramLaunched"));
        }
        catch (Exception ex)
        {
            AddLog(LocalizationService.Format("MainProgramLaunchFailed", ex.Message));
            MessageBox.Show(
                ex.Message,
                LocalizationService.GetString("LaunchErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AddLog(string message)
    {
        LogLines.Add(FormatLogTimestamp(message));
    }

    private void AddStartupLog(StartupLogType type, string? modeId = null, string? errorMessage = null)
    {
        if (type == StartupLogType.ModeSelected)
        {
            _startupLogEntries.RemoveAll(e => e.Type == StartupLogType.ModeSelected);
        }

        _startupLogEntries.Add(new StartupLogEntry(type, modeId, errorMessage));
        RefreshStartupLogs();
    }

    private void RefreshStartupLogs()
    {
        if (IsRunning || IsCompleted || _startupLogEntries.Count == 0)
        {
            return;
        }

        LogLines.Clear();
        foreach (var entry in _startupLogEntries)
        {
            LogLines.Add(FormatLogTimestamp(FormatStartupLogEntry(entry)));
        }
    }

    private static string FormatLogTimestamp(string message) =>
        $"[{DateTime.Now:HH:mm:ss}] {message}";

    private string FormatStartupLogEntry(StartupLogEntry entry) =>
        entry.Type switch
        {
            StartupLogType.ConfigLoaded => LocalizationService.Get("ConfigLoaded"),
            StartupLogType.ModeSelected when entry.ModeId is not null =>
                LocalizationService.Format(
                    "ModeSelectedLog",
                    GetLocalizedModeNameFromId(entry.ModeId)),
            StartupLogType.Error when entry.ErrorMessage is not null =>
                $"{LocalizationService.Get("ErrorPrefix")} {entry.ErrorMessage}",
            StartupLogType.PostRebootContinueArg =>
                LocalizationService.Get("ErrorFix_PostRebootContinueArgLog"),
            _ => string.Empty
        };

    private string GetLocalizedModeNameFromId(string modeId)
    {
        var mode = Modes.FirstOrDefault(m => m.Id == modeId);
        return mode is not null ? GetLocalizedModeName(mode.Mode) : modeId;
    }

    private enum StartupLogType
    {
        ConfigLoaded,
        ModeSelected,
        Error,
        PostRebootContinueArg
    }

    private sealed record StartupLogEntry(StartupLogType Type, string? ModeId = null, string? ErrorMessage = null);

    private static bool IsDefenderDisableStep(string stepId) =>
        stepId.Equals("disable-defender", StringComparison.OrdinalIgnoreCase) ||
        stepId.Equals("error-fix-disable-defender", StringComparison.OrdinalIgnoreCase);

    private void ApplyLocalization()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(OptionLabel));
        OnPropertyChanged(nameof(ResellerLabel));
        OnPropertyChanged(nameof(AppTagline));
        OnPropertyChanged(nameof(LogLabel));
        OnPropertyChanged(nameof(BtnStartInstall));
        OnPropertyChanged(nameof(BtnCancel));
        OnPropertyChanged(nameof(BtnLaunchMainProgram));
        OnPropertyChanged(nameof(BtnCheckForUpdates));
        OnPropertyChanged(nameof(AppVersionLabel));

        if (_config is not null)
        {
            AppTitle = LocalizationService.Get("AppName_Localized");
            Description = LocalizationService.Get("AppDescription_Localized");
        }

        foreach (var mode in Modes)
        {
            mode.RefreshLocalization();
        }

        foreach (var reseller in Resellers)
        {
            reseller.RefreshLocalization();
        }

        if (SelectedMode is not null)
        {
            SelectedModeDescription = GetLocalizedModeDescription(SelectedMode);
            var selectedVm = Modes.FirstOrDefault(m => m.Id == SelectedMode.Id);
            if (selectedVm is not null && !IsRunning && !IsCompleted)
            {
                StatusText = LocalizationService.Format("ModeSelectedStatus", GetLocalizedModeName(selectedVm.Mode));
            }
            else if (!IsRunning && !IsCompleted)
            {
                StatusText = LocalizationService.Get("PromptSelectModeAndStart");
            }

            if (!IsRunning)
            {
                LoadStepsForSelectedMode();
            }
            else
            {
                foreach (var step in Steps)
                {
                    step.RefreshLocalization();
                }
            }
        }
        else
        {
            StatusText = LocalizationService.Get("PromptSelectModeAndStart");
            SelectedModeDescription = LocalizationService.Get("PromptSelectOption");
        }

        RefreshStartupLogs();
    }

    public async Task TryAutoResumePostRebootAsync()
    {
        if (_postRebootResumeAttempted)
        {
            return;
        }

        _postRebootResumeAttempted = true;

        var pendingState = await Task.Run(ErrorFixStateService.IsPostRebootContinuation)
            .ConfigureAwait(true);
        var continueRequested = App.ContinueErrorFixRequested;

        if (continueRequested)
        {
            PostRebootAutoStartService.WriteAutoStartLog(
                LocalizationService.GetString("AutoStart_ContinueRequested"));
            AddStartupLog(StartupLogType.PostRebootContinueArg);
        }

        if (!pendingState)
        {
            if (continueRequested)
            {
                AddStartupLog(
                    StartupLogType.Error,
                    errorMessage: LocalizationService.Get("ErrorFix_NoPendingState"));
            }

            return;
        }

        await Task.Run(PostRebootAutoStartService.Cleanup).ConfigureAwait(true);

        PostRebootAutoStartService.WriteAutoStartLog(
            LocalizationService.GetString("AutoStart_ErrorFixResuming"));

        var pendingModeId = ErrorFixStateService.GetPendingModeId()
            ?? "error-fix";

        var resumeMode = Modes.FirstOrDefault(m =>
            m.Id.Equals(pendingModeId, StringComparison.OrdinalIgnoreCase));
        if (resumeMode is null || IsRunning)
        {
            return;
        }

        SelectMode(resumeMode.Id);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        MessageBox.Show(
            LocalizationService.GetString("ErrorFix_PostRebootAutoResumeMessage"),
            LocalizationService.GetString("ErrorFix_PostRebootAutoResumeTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await StartInstallAsync().ConfigureAwait(true);
    }

    public async Task CheckForUpdatesAsync(bool silent)
    {
        if (IsCheckingForUpdates || IsRunning)
        {
            return;
        }

        IsCheckingForUpdates = true;
        if (!silent)
        {
            StatusText = LocalizationService.Get("Update_Checking");
        }

        try
        {
            var result = await Task.Run(() => AutoUpdateService.CheckForUpdatesAsync())
                .ConfigureAwait(true);

            switch (result.Status)
            {
                case UpdateStatus.UpToDate:
                    if (!silent)
                    {
                        MessageBox.Show(
                            LocalizationService.Format(
                                "Update_UpToDate",
                                result.RemoteVersion?.ToString(3) ?? AutoUpdateService.CurrentVersionLabel),
                            LocalizationService.GetString("Update_UpToDateTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    break;

                case UpdateStatus.UpdateAvailable:
                    if (silent)
                    {
                        await ApplyUpdateSilentlyAsync(result).ConfigureAwait(true);
                    }
                    else
                    {
                        await PromptAndApplyUpdateAsync(result).ConfigureAwait(true);
                    }

                    break;

                case UpdateStatus.CheckFailed:
                    if (!silent)
                    {
                        MessageBox.Show(
                            LocalizationService.Format("Update_CheckFailed", result.ErrorMessage ?? string.Empty),
                            LocalizationService.GetString("Update_CheckFailedTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(
                    LocalizationService.Format("Update_CheckFailed", ex.Message),
                    LocalizationService.GetString("Update_CheckFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private async Task ApplyUpdateSilentlyAsync(UpdateCheckResult result)
    {
        StatusText = LocalizationService.Format(
            "Update_SilentApplying",
            result.RemoteVersion?.ToString(3) ?? string.Empty);

        var applyResult = await Task.Run(() =>
                AutoUpdateService.DownloadAndApplyUpdateAsync(result))
            .ConfigureAwait(true);

        if (!applyResult.Success)
        {
            return;
        }

        Application.Current.Shutdown();
    }

    private async Task PromptAndApplyUpdateAsync(UpdateCheckResult result)
    {
        var releaseNotes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? LocalizationService.Get("Update_NoReleaseNotes")
            : result.ReleaseNotes.Trim();

        var confirm = MessageBox.Show(
            LocalizationService.Format(
                "Update_AvailableMessage",
                result.RemoteVersion?.ToString(3) ?? string.Empty,
                Environment.NewLine,
                releaseNotes),
            LocalizationService.GetString("Update_AvailableTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        StatusText = LocalizationService.Get("Update_Downloading");
        var applyResult = await Task.Run(() =>
                AutoUpdateService.DownloadAndApplyUpdateAsync(result))
            .ConfigureAwait(true);

        if (!applyResult.Success)
        {
            MessageBox.Show(
                LocalizationService.Format("Update_DownloadFailed", applyResult.ErrorMessage ?? string.Empty),
                LocalizationService.GetString("Update_CheckFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(
            LocalizationService.GetString("Update_RestartPrompt"),
            LocalizationService.GetString("Update_RestartTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Application.Current.Shutdown();
    }

    private static string GetLocalizedModeName(InstallMode mode) =>
        LocalizationService.GetLocalizedModeName(mode.Id, mode.Name) ?? mode.Name;

    private static string GetLocalizedModeDescription(InstallMode mode) =>
        LocalizationService.GetLocalizedModeDescription(mode.Id, mode.Description) ?? mode.Description;

    private static string GetLocalizedResellerName(ResellerType reseller) =>
        LocalizationService.Get(reseller switch
        {
            ResellerType.Unkclub => "Reseller_unkclub_Name",
            ResellerType.OtherReseller => "Reseller_other_Name",
            _ => "Reseller_unkclub_Name"
        });

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ResellerOptionViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _name = string.Empty;
    private string _description = string.Empty;

    public ResellerOptionViewModel(ResellerType resellerType)
    {
        ResellerType = resellerType;
        Id = resellerType.ToString();
        RefreshLocalization();
    }

    public ResellerType ResellerType { get; }
    public string Id { get; }

    public string Name
    {
        get => _name;
        private set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Description
    {
        get => _description;
        private set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public void RefreshLocalization()
    {
        Name = LocalizationService.Get(ResellerType switch
        {
            ResellerType.Unkclub => "Reseller_unkclub_Name",
            ResellerType.OtherReseller => "Reseller_other_Name",
            _ => "Reseller_unkclub_Name"
        });

        Description = LocalizationService.Get(ResellerType switch
        {
            ResellerType.Unkclub => "Reseller_unkclub_Description",
            ResellerType.OtherReseller => "Reseller_other_Description",
            _ => "Reseller_unkclub_Description"
        });
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }

    public Brush BorderBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(139, 0, 0))
        : new SolidColorBrush(Color.FromRgb(51, 65, 85));

    public Brush BackgroundBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(59, 20, 28))
        : new SolidColorBrush(Color.FromRgb(51, 65, 85));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ModeOptionViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _name;
    private string _description;

    public ModeOptionViewModel(InstallMode mode)
    {
        Mode = mode;
        Id = mode.Id;
        _name = mode.Name;
        _description = mode.Description;
        RefreshLocalization();
    }

    public InstallMode Mode { get; }
    public string Id { get; }

    public string Name
    {
        get => _name;
        private set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Description
    {
        get => _description;
        private set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public void RefreshLocalization()
    {
        Name = LocalizationService.GetLocalizedModeName(Id, Mode.Name) ?? Mode.Name;
        Description = LocalizationService.GetLocalizedModeDescription(Id, Mode.Description) ?? Mode.Description;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }

    public Brush BorderBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
        : new SolidColorBrush(Color.FromRgb(51, 65, 85));

    public Brush BackgroundBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(30, 58, 95))
        : new SolidColorBrush(Color.FromRgb(51, 65, 85));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class StepItemViewModel : INotifyPropertyChanged
{
    private string _name;
    private string _description;

    public StepItemViewModel(InstallStep step)
    {
        Step = step;
        Id = step.Id;
        _name = step.Name;
        _description = step.Description ?? string.Empty;
        RefreshLocalization();
    }

    public InstallStep Step { get; }
    public string Id { get; }

    public string Name
    {
        get => _name;
        private set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Description
    {
        get => _description;
        private set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public string StatusText { get; private set; } = string.Empty;
    public Brush StatusBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(148, 163, 184));

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLocalization()
    {
        Name = LocalizationService.GetLocalizedStepName(Id, Step.Name);
        Description = LocalizationService.GetLocalizedStepDescription(Id, Step.Description);
    }

    public void Reset()
    {
        StatusText = LocalizationService.Get("StepPending");
        StatusBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }

    public void Update(StepProgress progress)
    {
        StatusText = progress.Message;
        StatusBrush = progress.Status switch
        {
            StepStatus.Running => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            StepStatus.Success => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            StepStatus.Skipped => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
            StepStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };

        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        _canExecute?.Invoke(parameter is T typed ? typed : default) ?? true;

    public void Execute(object? parameter) =>
        _execute(parameter is T typed ? typed : default);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
