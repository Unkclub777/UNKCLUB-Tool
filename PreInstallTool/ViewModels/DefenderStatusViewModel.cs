using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PreInstallTool.Localization;
using PreInstallTool.Models;
using PreInstallTool.Services;

namespace PreInstallTool.ViewModels;

public sealed class DefenderStatusViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _refreshTimer;
    private int _refreshInFlight;
    private string _lastUpdatedText = string.Empty;
    private string _defenderStatusTitle = string.Empty;
    private string _legendEnabled = string.Empty;
    private string _legendDisabled = string.Empty;

    public DefenderStatusViewModel()
    {
        Features = new ObservableCollection<FeatureStatusItem>();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();

        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();

        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Refresh);
    }

    public ObservableCollection<FeatureStatusItem> Features { get; }

    public string DefenderStatusTitle
    {
        get => _defenderStatusTitle;
        private set => SetProperty(ref _defenderStatusTitle, value);
    }

    public string LegendEnabled
    {
        get => _legendEnabled;
        private set => SetProperty(ref _legendEnabled, value);
    }

    public string LegendDisabled
    {
        get => _legendDisabled;
        private set => SetProperty(ref _legendDisabled, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        if (Interlocked.CompareExchange(ref _refreshInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            IReadOnlyList<FeatureStatusItem> latest;
            try
            {
                latest = DefenderStatusService.ReadAllFeatures();
            }
            catch
            {
                PublishRefreshFailure();
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                Interlocked.Exchange(ref _refreshInFlight, 0);
                return;
            }

            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    ApplyFeatures(latest);
                    LastUpdatedText = LocalizationService.Format(
                        "DefenderLastUpdated",
                        DateTime.Now.ToString("HH:mm:ss"));
                }
                catch
                {
                    LastUpdatedText = LocalizationService.Get("DefenderReadFailed");
                }
                finally
                {
                    Interlocked.Exchange(ref _refreshInFlight, 0);
                }
            });
        });
    }

    public void Dispose()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _refreshTimer.Stop();
    }

    private void PublishRefreshFailure()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            LastUpdatedText = LocalizationService.Get("DefenderReadFailed");
            Interlocked.Exchange(ref _refreshInFlight, 0);
        });
    }

    private void ApplyFeatures(IReadOnlyList<FeatureStatusItem> latest)
    {
        for (var index = 0; index < latest.Count; index++)
        {
            var localizedName = LocalizationService.GetLocalizedFeatureName(latest[index].Id);
            if (index < Features.Count)
            {
                Features[index].Id = latest[index].Id;
                Features[index].Name = localizedName;
                Features[index].IsEnabled = latest[index].IsEnabled;
            }
            else
            {
                latest[index].Name = localizedName;
                Features.Add(latest[index]);
            }
        }

        while (Features.Count > latest.Count)
        {
            Features.RemoveAt(Features.Count - 1);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        Refresh();
    }

    private void ApplyLocalization()
    {
        DefenderStatusTitle = LocalizationService.Get("DefenderStatusTitle");
        LegendEnabled = LocalizationService.Get("LegendEnabled");
        LegendDisabled = LocalizationService.Get("LegendDisabled");

        if (Features.Count == 0)
        {
            LastUpdatedText = LocalizationService.Get("DefenderNotReadYet");
        }
        else
        {
            foreach (var feature in Features)
            {
                feature.Name = LocalizationService.GetLocalizedFeatureName(feature.Id);
            }
        }
    }

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
