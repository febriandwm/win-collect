namespace WinCollect.Models;

public class SessionRecord
{
    public string SessionId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public int EventCount { get; set; }
}
