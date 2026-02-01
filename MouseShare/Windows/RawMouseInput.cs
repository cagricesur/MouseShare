using System.Runtime.InteropServices;

namespace MouseShare.Windows;

/// <summary>
/// Captures relative mouse movement via Raw Input (works when cursor is clamped at screen edge).
/// </summary>
public sealed class RawMouseInput : IDisposable
{
    private readonly Action<int, int> _onDelta;
    private nint _hwnd;
    private static readonly uint WM_INPUT = 0x00FF;
    private static readonly uint WM_APP_STOP = 0x8000;
    [ThreadStatic] private static RawMouseInput? s_current;

    private const int RIDEV_INPUTSINK = 0x00000100;
    private const int RIM_TYPEMOUSE = 0;
    private const int RID_INPUT = 0x10000003;
    private const int MOUSE_MOVE_RELATIVE = 0;

    public RawMouseInput(Action<int, int> onDelta)
    {
        _onDelta = onDelta;
    }

    public void Start()
    {
        var thread = new Thread(MessageLoop) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Stop()
    {
        if (_hwnd != 0)
            PostMessage(_hwnd, WM_APP_STOP, 0, 0);
    }

    private void MessageLoop()
    {
        s_current = this;
        var wndProc = new WndProcDelegate(WndProc);
        var gcHandle = GCHandle.Alloc(wndProc);

        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "MouseShareRawInput"
        };
        RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, "MouseShareRawInput", null, 0, 0, 0, 1, 1, (nint)(-3), 0, 0, 0);
        if (_hwnd == 0) { gcHandle.Free(); return; }

        var rid = new RAWINPUTDEVICE
        {
            usUsagePage = 0x01,
            usUsage = 0x02,
            dwFlags = RIDEV_INPUTSINK,
            hwndTarget = _hwnd
        };
        RegisterRawInputDevices([rid], 1, Marshal.SizeOf<RAWINPUTDEVICE>());

        while (GetMessage(out var msg, nint.Zero, 0, 0))
        {
            if (msg.message == WM_APP_STOP) break;
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        DestroyWindow(_hwnd);
        _hwnd = 0;
        gcHandle.Free();
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    // RAWINPUTHEADER: 24 bytes on 64-bit (dwType 4 + dwSize 4 + hDevice 8 + wParam 8)
    // RAWMOUSE: usFlags 2, union 4, ulRawButtons 4, lLastX 4, lLastY 4, ulExtraInfo 4
    // lLastX at headerSize+12, lLastY at headerSize+16
    private static readonly int s_headerSize = IntPtr.Size == 8 ? 24 : 16;
    private static readonly int s_lastXOffset = s_headerSize + 12;
    private static readonly int s_lastYOffset = s_headerSize + 16;

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_INPUT)
        {
            var buffer = new byte[64];
            var size = (uint)buffer.Length;
            var result = GetRawInputData(lParam, RID_INPUT, buffer, ref size, s_headerSize);
            if (result > 0 && size >= s_lastYOffset + 4)
                ParseRawInput(buffer);
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ParseRawInput(byte[] buffer)
    {
        if (buffer.Length < s_lastYOffset + 4) return;
        if (BitConverter.ToInt32(buffer, 0) != RIM_TYPEMOUSE) return;
        if ((BitConverter.ToUInt16(buffer, s_headerSize) & 0x01) != MOUSE_MOVE_RELATIVE) return; // usFlags
        var lastX = BitConverter.ToInt32(buffer, s_lastXOffset);
        var lastY = BitConverter.ToInt32(buffer, s_lastYOffset);
        _onDelta(lastX, lastY);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public int dwFlags;
        public nint hwndTarget;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern nint CreateWindowEx(int dwExStyle, string lpClassName, string? lpWindowName, int dwStyle, int x, int y, int w, int h, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(nint hRawInput, int uiCommand, byte[]? pData, ref uint pcbSize, int cbSizeHeader);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

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
    private static extern bool PostMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string? lpModuleName);

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
    private struct POINT
    {
        public int x, y;
    }

    public void Dispose() => Stop();
}
