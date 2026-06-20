using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using PreInstallTool.Localization;
using PreInstallTool.ViewModels;

namespace PreInstallTool;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Activated += (_, _) => _viewModel.DefenderStatus.Refresh();
        Closed += (_, _) => _viewModel.DefenderStatus.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            _ = RunAutoResumeSafelyAsync();
            _ = RunStartupUpdateCheckSafelyAsync();
        });
    }

    private async Task RunStartupUpdateCheckSafelyAsync()
    {
        try
        {
            await _viewModel.CheckForUpdatesOnStartupAsync().ConfigureAwait(true);
        }
        catch
        {
            // Startup update checks should never block startup.
        }
    }

    private async Task RunAutoResumeSafelyAsync()
    {
        try
        {
            await _viewModel.TryAutoResumePostRebootAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                LocalizationService.GetString("UnexpectedErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.WindowTitle))
        {
            Title = _viewModel.WindowTitle;
        }
    }
}
