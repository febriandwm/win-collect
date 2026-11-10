using Serilog;
using WinCollect.Infrastructure;
using WinCollect.Models;

namespace WinCollect.Services;

/// <summary>
/// Manages monitoring sessions — start, stop, and event buffering.
/// </summary>
public sealed class SessionService : IDisposable
{
    private readonly KeyboardHookService _keyboardHook;
    private readonly System.Timers.Timer _flushTimer;
    private SessionRecord? _currentSession;

    public event EventHandler<string>? SessionLogUpdated;

    private readonly List<string> _sessionLog = new();
    private readonly object _logLock = new();

    public SessionService()
    {
        _keyboardHook = new KeyboardHookService();
        _keyboardHook.KeyEventCaptured += OnKeyEventCaptured;

        // Flush buffered events to SQLite every 30 seconds
        _flushTimer = new System.Timers.Timer(30_000);
        _flushTimer.Elapsed += (_, _) => FlushEvents();
        _flushTimer.AutoReset = true;
    }

    public SessionRecord StartSession(string deviceId)
    {
        var sessionId = $"session-{DateTime.UtcNow:yyyyMMddHHmmss}";

        _currentSession = new SessionRecord
        {
            SessionId = sessionId,
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow,
            Status = "ACTIVE"
        };

        DatabaseService.InsertSession(_currentSession);
        AppState.CurrentSessionId = sessionId;
        AppState.IsMonitoring = true;

        lock (_logLock)
        {
            _sessionLog.Clear();
            _sessionLog.Add($"SESSION: {sessionId}");
            _sessionLog.Add($"DEVICE:  {AppState.ComputerName}");
            _sessionLog.Add($"Started: {_currentSession.StartedAt:HH:mm:ss} UTC");
            _sessionLog.Add(string.Empty);
        }

        _keyboardHook.Start();
        _flushTimer.Start();

        Log.Information("Session started: {SessionId}", sessionId);
        return _currentSession;
    }

    public void StopSession()
    {
        if (_currentSession == null) return;

        _flushTimer.Stop();
        _keyboardHook.Stop();

        // Final flush before closing session
        FlushEvents();

        var endedAt = DateTime.UtcNow;
        DatabaseService.EndSession(_currentSession.SessionId, endedAt);
        _currentSession.EndedAt = endedAt;
        _currentSession.Status = "COMPLETED";

        AppState.IsMonitoring = false;
        AppState.CurrentSessionId = null;

        Log.Information("Session ended: {SessionId}", _currentSession.SessionId);
        _currentSession = null;
    }

    private void OnKeyEventCaptured(object? sender, KeyEvent ev)
    {
        string line = $"[{ev.Timestamp:HH:mm:ss.fff}] {ev.EventType,-9} {ev.Key}";
        lock (_logLock)
        {
            _sessionLog.Add(line);
            // Keep last 50 lines for display
            if (_sessionLog.Count > 50)
                _sessionLog.RemoveAt(0);
        }
        SessionLogUpdated?.Invoke(this, line);
    }

    private void FlushEvents()
    {
        var events = _keyboardHook.FlushBuffer();
        if (events.Count == 0) return;

        DatabaseService.InsertEvents(events);
        Log.Debug("Flushed {Count} events to local DB", events.Count);
    }

    public string GetSessionLog()
    {
        lock (_logLock)
        {
            return string.Join(Environment.NewLine, _sessionLog.TakeLast(20));
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        _keyboardHook.Dispose();
    }
}
