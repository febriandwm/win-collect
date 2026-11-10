using Serilog;

namespace WinCollect.Services;

/// <summary>
/// Orchestrates periodic synchronization of local events to Firebase.
/// Handles offline queuing and retry logic.
/// </summary>
public sealed class SyncService : IDisposable
{
    private readonly FirebaseService _firebase;
    private readonly System.Timers.Timer _syncTimer;
    private bool _isSyncing;

    public SyncService(FirebaseService firebase)
    {
        _firebase = firebase;

        // Try syncing every 30 seconds
        _syncTimer = new System.Timers.Timer(30_000);
        _syncTimer.Elapsed += async (_, _) => await TrySyncAsync();
        _syncTimer.AutoReset = true;
    }

    public void Start() => _syncTimer.Start();
    public void Stop()  => _syncTimer.Stop();

    public async Task TrySyncAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            var pending = DatabaseService.GetPendingEvents(500);
            if (pending.Count == 0) return;

            Log.Debug("Attempting to sync {Count} pending events", pending.Count);
            var synced = await _firebase.SyncEventsAsync(pending);

            if (synced.Count > 0)
                DatabaseService.MarkEventsSynced(synced);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Sync error");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void Dispose() => _syncTimer.Dispose();
}
