namespace MS.Core.Models;

/// <summary>
/// Represents screen dimensions for coordinate mapping between different resolutions.
/// </summary>
public record ScreenInfo(int Width, int Height)
{
    public double AspectRatio => (double)Width / Height;
}
