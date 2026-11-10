using WinCollect.Models;
using Xunit;

namespace WinCollect.Tests;

public class KeyEventTests
{
    [Fact]
    public void KeyEvent_ShouldHaveCorrectDefaults()
    {
        var ev = new KeyEvent();
        Assert.Equal("PENDING", ev.SyncStatus);
        Assert.Equal(string.Empty, ev.EventId);
    }

    [Theory]
    [InlineData("KEY_DOWN")]
    [InlineData("KEY_UP")]
    public void KeyEvent_EventType_ShouldBeValid(string eventType)
    {
        var ev = new KeyEvent { EventType = eventType };
        Assert.Equal(eventType, ev.EventType);
    }

    [Fact]
    public void KeyEvent_ShouldSerializeToExpectedJson()
    {
        var ev = new KeyEvent
        {
            EventId = "test-event-id",
            SessionId = "session-001",
            DeviceId = "computer-001",
            Sequence = 1,
            Timestamp = new DateTime(2026, 11, 10, 10, 15, 1, DateTimeKind.Utc),
            EventType = "KEY_DOWN",
            Key = "H"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            deviceId = ev.DeviceId,
            sessionId = ev.SessionId,
            sequence = ev.Sequence,
            timestamp = ev.Timestamp.ToString("O"),
            eventType = ev.EventType,
            key = ev.Key
        });

        Assert.Contains("\"deviceId\":\"computer-001\"", json);
        Assert.Contains("\"eventType\":\"KEY_DOWN\"", json);
        Assert.Contains("\"key\":\"H\"", json);
    }
}
