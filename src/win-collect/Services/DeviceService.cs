using Serilog;
using WinCollect.Infrastructure;
using WinCollect.Models;
using System.Reflection;

namespace WinCollect.Services;

/// <summary>
/// Manages persistent device identity and registration.
/// </summary>
public static class DeviceService
{
    private const string DeviceIdSettingKey = "DeviceId";

    public static DeviceInfo LoadOrCreateDevice(string computerName)
    {
        // Try to load existing device ID from settings
        var existingId = SettingsService.Get(DeviceIdSettingKey);

        if (!string.IsNullOrEmpty(existingId))
        {
            var existing = DatabaseService.GetDevice(existingId);
            if (existing != null)
            {
                existing.ComputerName = computerName;
                existing.LastSeenAt = DateTime.UtcNow;
                AppState.DeviceId = existing.DeviceId;
                AppState.ComputerName = computerName;
                DatabaseService.UpsertDevice(existing);
                Log.Information("Loaded existing device: {DeviceId}", existing.DeviceId);
                return existing;
            }
        }

        // Create new device
        var device = new DeviceInfo
        {
            DeviceId = Guid.NewGuid().ToString("D"),
            ComputerName = computerName,
            OperatingSystem = GetOsVersion(),
            ApplicationVersion = GetAppVersion(),
            RegisteredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            MonitoringStatus = "STOPPED"
        };

        DatabaseService.UpsertDevice(device);
        SettingsService.Set(DeviceIdSettingKey, device.DeviceId);

        AppState.DeviceId = device.DeviceId;
        AppState.ComputerName = computerName;

        Log.Information("Created new device: {DeviceId}", device.DeviceId);
        return device;
    }

    private static string GetOsVersion()
    {
        try { return Environment.OSVersion.VersionString; }
        catch { return "Windows"; }
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "1.0.0";
    }
}
