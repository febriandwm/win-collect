using WinCollect.Services;
using WinCollect.Infrastructure;
using Serilog;
// Explicit WPF aliases — prevents ambiguity with System.Windows.Forms if ever referenced
using Application    = System.Windows.Application;
using MessageBox     = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage  = System.Windows.MessageBoxImage;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs    = System.Windows.ExitEventArgs;

namespace WinCollect;

public partial class App : Application
{
    private static Mutex? _mutex;
    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance enforcement
        const string mutexName = "Global\\WinCollect_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("win-collect is already running. Check the system tray.",
                "win-collect", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Initialize logging
        LoggingService.Initialize();
        Log.Information("win-collect starting up");

        // Initialize database
        DatabaseService.Initialize();

        base.OnStartup(e);

        // Create main window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        // Initialize tray icon service
        _trayIconService = new TrayIconService(mainWindow);

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("win-collect shutting down");
        _trayIconService?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
