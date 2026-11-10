using Hardcodet.Wpf.TaskbarNotification;
using WinCollect.Infrastructure;
// Explicit WPF aliases — guards against future ambiguity if WinForms ever re-enters the project
using Application  = System.Windows.Application;
using ContextMenu  = System.Windows.Controls.ContextMenu;
using MenuItem     = System.Windows.Controls.MenuItem;
using Separator    = System.Windows.Controls.Separator;

namespace WinCollect.Services;

/// <summary>
/// Manages the Windows system tray icon and context menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly MainWindow _mainWindow;

    public TrayIconService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "win-collect — Stopped",
            IconSource  = LoadIcon("Resources/tray-inactive.ico")
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();
        _trayIcon.ContextMenu = BuildContextMenu();
    }

    private static System.Windows.Media.ImageSource? LoadIcon(string relativePath)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            return new System.Windows.Media.Imaging.BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open win-collect" };
        openItem.Click += (_, _) => ShowWindow();
        menu.Items.Add(openItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    public void UpdateStatus(bool isMonitoring)
    {
        _trayIcon.ToolTipText = isMonitoring
            ? "win-collect — Monitoring Active"
            : "win-collect — Stopped";

        _trayIcon.IconSource = LoadIcon(isMonitoring
            ? "Resources/tray-active.ico"
            : "Resources/tray-inactive.ico");
    }

    private void ShowWindow() => _mainWindow.ShowWindow();

    private void ExitApplication()
    {
        AppState.IsExiting = true;
        _trayIcon.Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose() => _trayIcon.Dispose();
}
