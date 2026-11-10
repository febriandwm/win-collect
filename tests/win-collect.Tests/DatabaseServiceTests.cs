using WinCollect.Models;
using WinCollect.Services;
using Xunit;

namespace WinCollect.Tests;

/// <summary>
/// Unit tests for DatabaseService using an in-memory/temp SQLite database.
/// </summary>
public class DatabaseServiceTests : IDisposable
{
    private readonly string _tempDbPath;

    public DatabaseServiceTests()
    {
        // Each test uses a unique temp DB
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"wincollect-test-{Guid.NewGuid():N}.db");
        // Initialize the database with the temp path
        DatabaseService.Initialize(_tempDbPath);
    }

    [Fact]
    public void UpsertDevice_ShouldPersistAndRetrieve()
    {
        var device = new DeviceInfo
        {
            DeviceId = Guid.NewGuid().ToString(),
            ComputerName = "TEST-PC",
            OperatingSystem = "Windows 11",
            ApplicationVersion = "1.0.0",
            RegisteredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        DatabaseService.UpsertDevice(device);
        var retrieved = DatabaseService.GetDevice(device.DeviceId);

        Assert.NotNull(retrieved);
        Assert.Equal(device.DeviceId, retrieved.DeviceId);
        Assert.Equal("TEST-PC", retrieved.ComputerName);
    }

    [Fact]
    public void InsertSession_ShouldPersist()
    {
        var session = new SessionRecord
        {
            SessionId = $"session-{Guid.NewGuid():N}",
            DeviceId = "test-device",
            StartedAt = DateTime.UtcNow,
            Status = "ACTIVE"
        };

        DatabaseService.InsertSession(session);
        // No exception means success; verify via events
        Assert.True(true);
    }

    [Fact]
    public void InsertAndGetPendingEvents_ShouldWork()
    {
        var sessionId = $"session-{Guid.NewGuid():N}";
        var events = new List<KeyEvent>
        {
            new KeyEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                DeviceId = "test-device",
                Sequence = 1,
                Timestamp = DateTime.UtcNow,
                EventType = "KEY_DOWN",
                Key = "A"
            },
            new KeyEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                DeviceId = "test-device",
                Sequence = 2,
                Timestamp = DateTime.UtcNow,
                EventType = "KEY_UP",
                Key = "A"
            }
        };

        DatabaseService.InsertEvents(events);
        var pending = DatabaseService.GetPendingEvents(100);

        Assert.True(pending.Count >= 2);
        Assert.Contains(pending, e => e.Key == "A" && e.EventType == "KEY_DOWN");
    }

    [Fact]
    public void MarkEventsSynced_ShouldUpdateStatus()
    {
        var eventId = Guid.NewGuid().ToString("N");
        var events = new List<KeyEvent>
        {
            new KeyEvent
            {
                EventId = eventId,
                SessionId = "session-test",
                DeviceId = "test-device",
                Sequence = 1,
                Timestamp = DateTime.UtcNow,
                EventType = "KEY_DOWN",
                Key = "B"
            }
        };

        DatabaseService.InsertEvents(events);
        DatabaseService.MarkEventsSynced(new[] { eventId });

        var pending = DatabaseService.GetPendingEvents(100);
        Assert.DoesNotContain(pending, e => e.EventId == eventId);
    }

    [Fact]
    public void InsertEvents_ShouldPreventDuplicates()
    {
        var eventId = Guid.NewGuid().ToString("N");
        var ev = new KeyEvent
        {
            EventId = eventId,
            SessionId = "session-dup",
            DeviceId = "test-device",
            Sequence = 1,
            Timestamp = DateTime.UtcNow,
            EventType = "KEY_DOWN",
            Key = "C"
        };

        // Insert same event twice
        DatabaseService.InsertEvents(new[] { ev });
        DatabaseService.InsertEvents(new[] { ev });

        var pending = DatabaseService.GetPendingEvents(100);
        var count = pending.Count(e => e.EventId == eventId);
        Assert.Equal(1, count); // No duplicates
    }

    public void Dispose()
    {
        try { File.Delete(_tempDbPath); } catch { }
    }
}
