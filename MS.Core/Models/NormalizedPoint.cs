namespace MS.Core.Models;

/// <summary>
/// Point in normalized coordinates (0-1) for resolution-independent mapping.
/// </summary>
public record struct NormalizedPoint(double X, double Y)
{
    public static NormalizedPoint FromPixel(int x, int y, ScreenInfo screen) =>
        new((double)x / screen.Width, (double)y / screen.Height);

    public (int X, int Y) ToPixel(ScreenInfo screen) =>
        ((int)(X * screen.Width), (int)(Y * screen.Height));
}
