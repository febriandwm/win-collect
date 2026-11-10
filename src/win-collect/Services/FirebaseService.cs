using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;
using WinCollect.Models;

namespace WinCollect.Services;

/// <summary>
/// Communicates with Firebase via REST API (Firestore + Anonymous Auth).
/// No native C# Firebase SDK required — uses HttpClient against REST endpoints.
/// </summary>
public sealed class FirebaseService : IDisposable
{
    // Firebase project configuration (client-side config — safe to include in app)
    private const string ApiKey     = "AIzaSyDMC8KPtjkKcQXOElAnX01UeYuwEaD_ooE";
    private const string ProjectId  = "win-collect";
    private const string FirestoreBase = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";
    private const string AuthBase   = "https://identitytoolkit.googleapis.com/v1";

    private readonly HttpClient _http;
    private string? _idToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string Status { get; private set; } = "DISCONNECTED";
    public event EventHandler<string>? StatusChanged;

    public FirebaseService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ── Authentication ───────────────────────────────────────────────────────

    /// <summary>
    /// Signs in anonymously to get a Firebase ID token for Firestore writes.
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            SetStatus("AUTHENTICATING");
            var url  = $"{AuthBase}/accounts:signUp?key={ApiKey}";
            var body = JsonSerializer.Serialize(new { returnSecureToken = true });
            var resp = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                _idToken    = doc.RootElement.GetProperty("idToken").GetString();
                var expires = int.Parse(doc.RootElement.GetProperty("expiresIn").GetString() ?? "3600");
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expires - 60);
                SetStatus("CONNECTED");
                Log.Information("Firebase anonymous auth successful");
                return true;
            }

            Log.Warning("Firebase auth failed: {Code}", resp.StatusCode);
            SetStatus("ERROR");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Firebase authentication error");
            SetStatus("DISCONNECTED");
            return false;
        }
    }

    private async Task EnsureAuthAsync()
    {
        if (string.IsNullOrEmpty(_idToken) || DateTime.UtcNow >= _tokenExpiry)
            await AuthenticateAsync();
    }

    // ── Device Registration ───────────────────────────────────────────────────

    public async Task RegisterDeviceAsync(DeviceInfo device)
    {
        try
        {
            await EnsureAuthAsync();
            var url = $"{FirestoreBase}/devices/{device.DeviceId}?key={ApiKey}";
            var fields = BuildFields(new Dictionary<string, object>
            {
                ["computerName"]       = device.ComputerName,
                ["operatingSystem"]    = device.OperatingSystem,
                ["applicationVersion"] = device.ApplicationVersion,
                ["registeredAt"]       = device.RegisteredAt,
                ["lastSeenAt"]         = device.LastSeenAt,
                ["monitoringStatus"]   = device.MonitoringStatus
            });

            await PatchAsync(url, fields);
            Log.Information("Device registered in Firestore: {DeviceId}", device.DeviceId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to register device");
        }
    }

    // ── Session Sync ─────────────────────────────────────────────────────────

    public async Task SyncSessionAsync(SessionRecord session)
    {
        try
        {
            await EnsureAuthAsync();
            var url = $"{FirestoreBase}/devices/{session.DeviceId}/sessions/{session.SessionId}?key={ApiKey}";

            var dict = new Dictionary<string, object>
            {
                ["startedAt"]  = session.StartedAt,
                ["status"]     = session.Status,
                ["eventCount"] = session.EventCount
            };
            if (session.EndedAt.HasValue)
                dict["endedAt"] = session.EndedAt.Value;

            await PatchAsync(url, BuildFields(dict));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync session");
        }
    }

    // ── Event Batch Sync ─────────────────────────────────────────────────────

    public async Task<List<string>> SyncEventsAsync(List<KeyEvent> events)
    {
        var synced = new List<string>();
        if (events.Count == 0) return synced;

        try
        {
            await EnsureAuthAsync();
            SetStatus("SYNCHRONIZING");

            var writes = events.Select(ev => new
            {
                update = new
                {
                    name   = $"projects/{ProjectId}/databases/(default)/documents" +
                             $"/devices/{ev.DeviceId}/sessions/{ev.SessionId}/events/{ev.EventId}",
                    fields = new Dictionary<string, object>
                    {
                        ["sequence"]  = new { integerValue  = ev.Sequence.ToString() },
                        ["timestamp"] = new { timestampValue = ev.Timestamp.ToString("O") },
                        ["eventType"] = new { stringValue   = ev.EventType },
                        ["key"]       = new { stringValue   = ev.Key }
                    }
                }
            }).ToList();

            var batchUrl = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents:batchWrite?key={ApiKey}";
            var body     = JsonSerializer.Serialize(new { writes });
            var req      = BuildRequest(HttpMethod.Post, batchUrl, body);
            var resp     = await _http.SendAsync(req);

            if (resp.IsSuccessStatusCode)
            {
                synced.AddRange(events.Select(e => e.EventId));
                Log.Information("Synced {Count} events to Firestore", events.Count);
                SetStatus("CONNECTED");
            }
            else
            {
                var err = await resp.Content.ReadAsStringAsync();
                Log.Warning("Batch sync failed: {Code} - {Err}", resp.StatusCode, err);
                SetStatus("ERROR");
            }
        }
        catch (HttpRequestException)
        {
            SetStatus("DISCONNECTED");
            Log.Warning("No internet — events queued locally");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Event sync error");
            SetStatus("ERROR");
        }

        return synced;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task PatchAsync(string url, object fields)
    {
        var body = JsonSerializer.Serialize(new { fields });
        var req  = BuildRequest(HttpMethod.Patch, url, body);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            Log.Warning("PATCH {Url} failed: {Code}", url, resp.StatusCode);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string body)
    {
        var req = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(_idToken))
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _idToken);
        return req;
    }

    /// <summary>Converts a plain dictionary to Firestore field format.</summary>
    private static Dictionary<string, object> BuildFields(Dictionary<string, object> values)
    {
        var fields = new Dictionary<string, object>();
        foreach (var kv in values)
        {
            fields[kv.Key] = kv.Value switch
            {
                string s   => new { stringValue    = s },
                DateTime d => new { timestampValue = d.ToString("O") },
                int i      => new { integerValue   = i.ToString() },
                bool b     => new { booleanValue   = b },
                _          => new { stringValue    = kv.Value.ToString() ?? string.Empty }
            };
        }
        return fields;
    }

    private void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose() => _http.Dispose();
}
