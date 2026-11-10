using Microsoft.Data.Sqlite;
using System.IO;

namespace WinCollect.Services;

/// <summary>
/// Simple key-value settings store backed by SQLite.
/// </summary>
public static class SettingsService
{
    private static string _connectionString = string.Empty;

    static SettingsService()
    {
        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "win-collect");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "win-collect.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureTable();
    }

    private static void EnsureTable()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public static string? Get(string key)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar()?.ToString();
    }

    public static void Set(string key, string value)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
}
