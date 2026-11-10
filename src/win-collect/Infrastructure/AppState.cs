namespace WinCollect.Infrastructure;

/// <summary>
/// Global application state flags shared across services.
/// </summary>
public static class AppState
{
    /// <summary>Set to true before calling Application.Current.Shutdown() to allow the window to actually close.</summary>
    public static bool IsExiting { get; set; } = false;

    /// <summary>Indicates if keyboard monitoring is currently active.</summary>
    public static bool IsMonitoring { get; set; } = false;

    /// <summary>Current active session identifier.</summary>
    public static string? CurrentSessionId { get; set; }

    /// <summary>Persistent device identifier loaded at startup.</summary>
    public static string? DeviceId { get; set; }

    /// <summary>Human-readable computer name.</summary>
    public static string ComputerName { get; set; } = Environment.MachineName;
}
