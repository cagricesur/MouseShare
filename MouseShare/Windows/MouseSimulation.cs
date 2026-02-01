using System.Runtime.InteropServices;

namespace MouseShare.Windows;

/// <summary>
/// Simulates mouse input (clicks, scroll) on Windows via SendInput.
/// </summary>
internal static class MouseSimulation
{
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    public static void MouseButtonDown(int button)
    {
        var flags = button switch
        {
            0 => MOUSEEVENTF_LEFTDOWN,
            1 => MOUSEEVENTF_RIGHTDOWN,
            2 => MOUSEEVENTF_MIDDLEDOWN,
            _ => 0u
        };
        if (flags != 0)
            SendMouseInput(flags);
    }

    public static void MouseButtonUp(int button)
    {
        var flags = button switch
        {
            0 => MOUSEEVENTF_LEFTUP,
            1 => MOUSEEVENTF_RIGHTUP,
            2 => MOUSEEVENTF_MIDDLEUP,
            _ => 0u
        };
        if (flags != 0)
            SendMouseInput(flags);
    }

    public static void MouseScroll(int delta)
    {
        SendMouseInput(MOUSEEVENTF_WHEEL, (uint)delta);
    }

    private static void SendMouseInput(uint flags, uint mouseData = 0)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = 0,
                dy = 0,
                mouseData = mouseData,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = 0
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}
