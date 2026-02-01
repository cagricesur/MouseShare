using System.Runtime.InteropServices;
using MS.Core.Models;

namespace MouseShare.Windows;

/// <summary>
/// Captures mouse position and provides screen info on Windows.
/// </summary>
public static class MouseCapture
{
    public static (int X, int Y) GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }

    public static void SetCursorPosition(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
    }

    /// <summary>
    /// Gets the primary screen dimensions. For multi-monitor, uses virtual screen.
    /// </summary>
    public static ScreenInfo GetPrimaryScreenInfo()
    {
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        if (width <= 0 || height <= 0)
        {
            width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        }
        return new ScreenInfo(width, height);
    }

    /// <summary>
    /// Clamps coordinates to screen bounds.
    /// </summary>
    public static (int X, int Y) ClampToScreen(int x, int y, ScreenInfo screen)
    {
        return (
            Math.Clamp(x, 0, screen.Width - 1),
            Math.Clamp(y, 0, screen.Height - 1)
        );
    }
}
