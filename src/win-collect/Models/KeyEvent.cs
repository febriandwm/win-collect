namespace WinCollect.Models;

public class KeyEvent
{
    public string EventId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // KEY_DOWN or KEY_UP
    public string Key { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = "PENDING"; // PENDING, SYNCED, FAILED
}
