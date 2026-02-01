using System.Runtime.InteropServices;

namespace MouseShare.Windows;

/// <summary>
/// Low-level mouse hook to capture clicks and scroll when cursor is on Client.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly LowLevelMouseProc _proc;
    private nint _hookId = 0;
    private GCHandle _gcHandle;
    private Func<int, bool, bool>? _onButton; // returns true to consume (block) the event
    private Func<int, bool>? _onScroll;       // returns true to consume

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;

    public MouseHook()
    {
        _proc = HookCallback;
        _gcHandle = GCHandle.Alloc(_proc);
    }

    public void SetCallbacks(Func<int, bool, bool> onButton, Func<int, bool> onScroll)
    {
        _onButton = onButton;
        _onScroll = onScroll;
    }

    public void Install()
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var mod = curProcess.MainModule;
        var modHandle = mod != null ? GetModuleHandle(mod.ModuleName) : 0;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, modHandle, 0);
    }

    public void Uninstall()
    {
        if (_hookId != 0)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    public void Dispose()
    {
        Uninstall();
        _gcHandle.Free();
    }
}
