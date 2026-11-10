using Microsoft.Data.Sqlite;
using Serilog;
using System.IO;
using WinCollect.Models;

namespace WinCollect.Services;

/// <summary>
/// Manages local SQLite storage for device config, sessions, and pending sync events.
/// </summary>
public static class DatabaseService
{
    private static string _connectionString = string.Empty;

    /// <summary>Initialize using default app data path.</summary>
    public static void Initialize()
    {
        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "win-collect");
        Directory.CreateDirectory(dbDir);
        Initialize(Path.Combine(dbDir, "win-collect.db"));
    }

    /// <summary>Initialize with a specific database path (for testing).</summary>
    public static void Initialize(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Log.Debug("Database path: {DbPath}", dbPath);
        CreateTables();
    }

    private static void CreateTables()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Devices (
                DeviceId TEXT PRIMARY KEY,
                ComputerName TEXT NOT NULL,
                OperatingSystem TEXT NOT NULL,
                ApplicationVersion TEXT NOT NULL,
                RegisteredAt TEXT NOT NULL,
                LastSeenAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Sessions (
                SessionId TEXT PRIMARY KEY,
                DeviceId TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT,
                Status TEXT NOT NULL DEFAULT 'ACTIVE',
                EventCount INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS PendingEvents (
                EventId TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                Sequence INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Key TEXT NOT NULL,
                SyncStatus TEXT NOT NULL DEFAULT 'PENDING'
            );

            CREATE INDEX IF NOT EXISTS idx_pending_events_sync ON PendingEvents(SyncStatus);
            CREATE INDEX IF NOT EXISTS idx_pending_events_session ON PendingEvents(SessionId);
            """;
        cmd.ExecuteNonQuery();
        Log.Debug("Database tables ensured");
    }

    // ── Device ──────────────────────────────────────────────────────────────

    public static DeviceInfo? GetDevice(string deviceId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Devices WHERE DeviceId = $id";
        cmd.Parameters.AddWithValue("$id", deviceId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new DeviceInfo
        {
            DeviceId = reader.GetString(0),
            ComputerName = reader.GetString(1),
            OperatingSystem = reader.GetString(2),
            ApplicationVersion = reader.GetString(3),
            RegisteredAt = DateTime.Parse(reader.GetString(4)),
            LastSeenAt = DateTime.Parse(reader.GetString(5))
        };
    }

    public static void UpsertDevice(DeviceInfo device)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Devices (DeviceId, ComputerName, OperatingSystem, ApplicationVersion, RegisteredAt, LastSeenAt)
            VALUES ($id, $name, $os, $ver, $reg, $seen)
            ON CONFLICT(DeviceId) DO UPDATE SET
                ComputerName = excluded.ComputerName,
                LastSeenAt = excluded.LastSeenAt;
            """;
        cmd.Parameters.AddWithValue("$id", device.DeviceId);
        cmd.Parameters.AddWithValue("$name", device.ComputerName);
        cmd.Parameters.AddWithValue("$os", device.OperatingSystem);
        cmd.Parameters.AddWithValue("$ver", device.ApplicationVersion);
        cmd.Parameters.AddWithValue("$reg", device.RegisteredAt.ToString("O"));
        cmd.Parameters.AddWithValue("$seen", device.LastSeenAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    // ── Sessions ─────────────────────────────────────────────────────────────

    public static void InsertSession(SessionRecord session)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO Sessions (SessionId, DeviceId, StartedAt, EndedAt, Status, EventCount)
            VALUES ($sid, $did, $start, $end, $status, $count);
            """;
        cmd.Parameters.AddWithValue("$sid", session.SessionId);
        cmd.Parameters.AddWithValue("$did", session.DeviceId);
        cmd.Parameters.AddWithValue("$start", session.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$end", session.EndedAt.HasValue ? session.EndedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("$status", session.Status);
        cmd.Parameters.AddWithValue("$count", session.EventCount);
        cmd.ExecuteNonQuery();
    }

    public static void EndSession(string sessionId, DateTime endedAt)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Sessions
            SET EndedAt = $end, Status = 'COMPLETED',
                EventCount = (SELECT COUNT(*) FROM PendingEvents WHERE SessionId = $sid)
            WHERE SessionId = $sid;
            """;
        cmd.Parameters.AddWithValue("$end", endedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.ExecuteNonQuery();
    }

    // ── Events ───────────────────────────────────────────────────────────────

    public static void InsertEvents(IEnumerable<KeyEvent> events)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        foreach (var ev in events)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO PendingEvents (EventId, SessionId, DeviceId, Sequence, Timestamp, EventType, Key, SyncStatus)
                VALUES ($eid, $sid, $did, $seq, $ts, $type, $key, 'PENDING');
                """;
            cmd.Parameters.AddWithValue("$eid", ev.EventId);
            cmd.Parameters.AddWithValue("$sid", ev.SessionId);
            cmd.Parameters.AddWithValue("$did", ev.DeviceId);
            cmd.Parameters.AddWithValue("$seq", ev.Sequence);
            cmd.Parameters.AddWithValue("$ts", ev.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$type", ev.EventType);
            cmd.Parameters.AddWithValue("$key", ev.Key);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static List<KeyEvent> GetPendingEvents(int limit = 500)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PendingEvents WHERE SyncStatus = 'PENDING' ORDER BY Sequence LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<KeyEvent>();
        while (reader.Read())
        {
            list.Add(new KeyEvent
            {
                EventId = reader.GetString(0),
                SessionId = reader.GetString(1),
                DeviceId = reader.GetString(2),
                Sequence = reader.GetInt32(3),
                Timestamp = DateTime.Parse(reader.GetString(4)),
                EventType = reader.GetString(5),
                Key = reader.GetString(6),
                SyncStatus = reader.GetString(7)
            });
        }
        return list;
    }

    public static void MarkEventsSynced(IEnumerable<string> eventIds)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        foreach (var id in eventIds)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE PendingEvents SET SyncStatus = 'SYNCED' WHERE EventId = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
