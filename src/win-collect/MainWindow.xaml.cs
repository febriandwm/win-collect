using System.Windows;
using WinCollect.Infrastructure;

namespace WinCollect;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Intercept close to minimize to tray, unless the app is explicitly exiting
        if (!AppState.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
