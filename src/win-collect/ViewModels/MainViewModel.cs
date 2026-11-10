using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;
using WinCollect.Infrastructure;
using WinCollect.Models;
using WinCollect.Services;

namespace WinCollect.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly FirebaseService _firebase;
    private readonly SessionService  _session;
    private readonly SyncService     _sync;

    private string _computerName    = string.Empty;
    private string _deviceId        = "Loading…";
    private string _monitoringStatus = "STOPPED";
    private string _firebaseStatus  = "DISCONNECTED";
    private string _sessionInfo     = "No active session.";
    private bool   _startWithWindows;

    public MainViewModel()
    {
        _firebase = new FirebaseService();
        _session  = new SessionService();
        _sync     = new SyncService(_firebase);

        _firebase.StatusChanged     += (_, s) => FirebaseStatus = s;
        _session.SessionLogUpdated  += (_, _) => RefreshSessionInfo();

        StartCommand           = new RelayCommand(async () => await StartMonitoringAsync(), () => !AppState.IsMonitoring);
        StopCommand            = new RelayCommand(async () => await StopMonitoringAsync(),  () =>  AppState.IsMonitoring);
        SaveComputerNameCommand = new RelayCommand(SaveComputerName);

        _ = InitializeAsync();
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public string ComputerName
    {
        get => _computerName;
        set { _computerName = value; OnPropertyChanged(); }
    }

    public string DeviceId
    {
        get => _deviceId;
        set { _deviceId = value; OnPropertyChanged(); }
    }

    public string MonitoringStatus
    {
        get => _monitoringStatus;
        set
        {
            _monitoringStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MonitoringStatusColor));
        }
    }

    public string FirebaseStatus
    {
        get => _firebaseStatus;
        set
        {
            _firebaseStatus = value;
            Dispatch(() =>
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(FirebaseStatusColor));
            });
        }
    }

    public string SessionInfo
    {
        get => _sessionInfo;
        set { _sessionInfo = value; OnPropertyChanged(); }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            _startWithWindows = value;
            StartupService.SetStartupEnabled(value);
            OnPropertyChanged();
        }
    }

    public SolidColorBrush MonitoringStatusColor =>
        MonitoringStatus == "ACTIVE"
            ? new SolidColorBrush(Color.FromRgb(0, 176, 155))
            : new SolidColorBrush(Color.FromRgb(100, 116, 139));

    public SolidColorBrush FirebaseStatusColor => FirebaseStatus switch
    {
        "CONNECTED"      => new SolidColorBrush(Color.FromRgb(0, 176, 155)),
        "SYNCHRONIZING"  => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        "AUTHENTICATING" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
        "ERROR"          => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _                => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand StartCommand            { get; }
    public ICommand StopCommand             { get; }
    public ICommand SaveComputerNameCommand { get; }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        try
        {
            var savedName = SettingsService.Get("ComputerName") ?? Environment.MachineName;
            var device    = DeviceService.LoadOrCreateDevice(savedName);

            Dispatch(() =>
            {
                ComputerName      = device.ComputerName;
                DeviceId          = device.DeviceId;
                _startWithWindows = StartupService.IsStartupEnabled();
                OnPropertyChanged(nameof(StartWithWindows));
            });

            await _firebase.AuthenticateAsync();
            await _firebase.RegisterDeviceAsync(device);
            _sync.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Initialization error");
        }
    }

    private async Task StartMonitoringAsync()
    {
        try
        {
            var deviceId = AppState.DeviceId;
            if (string.IsNullOrEmpty(deviceId))
            {
                MessageBox.Show("Device not registered. Please save a computer name first.",
                    "win-collect", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sess = _session.StartSession(deviceId);
            await _firebase.SyncSessionAsync(sess);

            MonitoringStatus = "ACTIVE";
            RaiseCommandsChanged();

            // Minimize to system tray
            Application.Current?.MainWindow?.Hide();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start monitoring");
            MessageBox.Show($"Failed to start: {ex.Message}", "win-collect",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopMonitoringAsync()
    {
        try
        {
            _session.StopSession();
            await _sync.TrySyncAsync(); // immediate sync on stop

            MonitoringStatus = "STOPPED";
            SessionInfo      = "Session ended. Data synchronized.";
            RaiseCommandsChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to stop monitoring");
        }
    }

    private void SaveComputerName()
    {
        if (string.IsNullOrWhiteSpace(ComputerName))
        {
            MessageBox.Show("Computer name cannot be empty.", "win-collect",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SettingsService.Set("ComputerName", ComputerName);
        AppState.ComputerName = ComputerName;
        Log.Information("Computer name saved: {Name}", ComputerName);
    }

    private void RefreshSessionInfo()
    {
        Dispatch(() => SessionInfo = _session.GetSessionLog());
    }

    private void RaiseCommandsChanged()
    {
        ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
    }

    private static void Dispatch(Action action)
        => Application.Current?.Dispatcher.Invoke(action);

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
