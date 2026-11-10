using Microsoft.Win32;
using Serilog;

namespace WinCollect.Services;

/// <summary>
/// Manages Windows startup registration via the registry (current user only, no admin rights needed).
/// </summary>
public static class StartupService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName     = "win-collect";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check startup registry");
            return false;
        }
    }

    public static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    Log.Information("Windows startup enabled: {ExePath}", exePath);
                }
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Log.Information("Windows startup disabled");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set startup registry");
        }
    }
}
