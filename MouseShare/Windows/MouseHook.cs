using System.Runtime.InteropServices;

namespace MouseShare.Windows;

/// <summary>
/// Low-level mouse hook to capture clicks and scroll when cursor is on Client.
/// Must run on a thread with a message loop for the hook to receive events.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly Func<int, bool, bool>? _onButton; // returns true to consume (block) the event
    private readonly Func<int, bool>? _onScroll;       // returns true to consume
    private nint _hookId;
    private Thread? _thread;
    private volatile uint _hookThreadId;
    private bool _running;
    private static readonly uint WM_APP_STOP = 0x8000;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;

    public MouseHook(Func<int, bool, bool> onButton, Func<int, bool> onScroll)
    {
        _onButton = onButton;
        _onScroll = onScroll;
    }

    public void Install()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(MessageLoopWithHook) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Uninstall()
    {
        _running = false;
        if (_thread != null && _thread.IsAlive)
        {
            try { PostThreadMessage(_hookThreadId, WM_APP_STOP, 0, 0); } catch { }
            _thread.Join(500);
        }
        if (_hookId != 0)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }
    }

    private void MessageLoopWithHook()
    {
        _hookThreadId = GetCurrentThreadId();
        var proc = new LowLevelMouseProc(HookCallback);
        var gcHandle = GCHandle.Alloc(proc);
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, proc, nint.Zero, 0);

        if (_hookId == 0)
        {
            gcHandle.Free();
            return;
        }

        while (_running && GetMessage(out var msg, nint.Zero, 0, 0))
        {
            if (msg.message == WM_APP_STOP) break;
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookId != 0)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }
        gcHandle.Free();
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN: if (_onButton?.Invoke(0, true) == true) return (nint)1; break;
                case WM_LBUTTONUP: if (_onButton?.Invoke(0, false) == true) return (nint)1; break;
                case WM_RBUTTONDOWN: if (_onButton?.Invoke(1, true) == true) return (nint)1; break;
                case WM_RBUTTONUP: if (_onButton?.Invoke(1, false) == true) return (nint)1; break;
                case WM_MBUTTONDOWN: if (_onButton?.Invoke(2, true) == true) return (nint)1; break;
                case WM_MBUTTONUP: if (_onButton?.Invoke(2, false) == true) return (nint)1; break;
                case WM_MOUSEWHEEL:
                    var delta = (short)(((long)wParam >> 16) & 0xFFFF);
                    if (_onScroll?.Invoke(delta) == true) return (nint)1;
                    break;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    public void Dispose()
    {
        Uninstall();
    }
}
