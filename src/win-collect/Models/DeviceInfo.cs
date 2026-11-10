namespace WinCollect.Models;

public class DeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string ApplicationVersion { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string MonitoringStatus { get; set; } = "STOPPED";
}
