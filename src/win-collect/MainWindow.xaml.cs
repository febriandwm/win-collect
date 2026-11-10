using WinCollect.Infrastructure;
using Application = System.Windows.Application;
using Window      = System.Windows.Window;

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
        WindowState = System.Windows.WindowState.Normal;
        Activate();
    }
}
