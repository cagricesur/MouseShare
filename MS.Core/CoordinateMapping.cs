using MS.Core.Models;

namespace MS.Core;

/// <summary>
/// Handles coordinate mapping between Host and Client screens with different resolutions.
/// Uses normalized coordinates (0-1) for resolution-independent transitions.
/// </summary>
public static class CoordinateMapping
{
    /// <summary>
    /// Edge transition threshold - cursor must be within this normalized distance from edge.
    /// </summary>
    public const double EdgeThreshold = 0.01;

    /// <summary>
    /// Detects if the cursor is at a screen edge that would transition to the other PC.
    /// Host right edge → Client; Client left edge → Host (when cursor was on client side).
    /// </summary>
    public static Edge DetectEdge(double normalizedX, double normalizedY)
    {
        var edge = Edge.None;
        if (normalizedX <= EdgeThreshold) edge |= Edge.Left;
        if (normalizedX >= 1 - EdgeThreshold) edge |= Edge.Right;
        if (normalizedY <= EdgeThreshold) edge |= Edge.Top;
        if (normalizedY >= 1 - EdgeThreshold) edge |= Edge.Bottom;
        return edge;
    }

    /// <summary>
    /// When crossing from Host right edge to Client, map Y and set X=0 (left edge of client).
    /// </summary>
    public static NormalizedPoint MapHostRightToClientLeft(double hostY) =>
        new(0, Math.Clamp(hostY, 0, 1));

    /// <summary>
    /// When crossing from Host left edge to Client, map Y and set X=1 (right edge of client).
    /// </summary>
    public static NormalizedPoint MapHostLeftToClientRight(double hostY) =>
        new(1, Math.Clamp(hostY, 0, 1));

    /// <summary>
    /// When crossing from Client left edge to Host, map Y and set X=1 (right edge of host).
    /// </summary>
    public static NormalizedPoint MapClientLeftToHostRight(double clientY) =>
        new(1, Math.Clamp(clientY, 0, 1));

    /// <summary>
    /// When crossing from Client right edge to Host, map Y and set X=0 (left edge of host).
    /// </summary>
    public static NormalizedPoint MapClientRightToHostLeft(double clientY) =>
        new(0, Math.Clamp(clientY, 0, 1));

    /// <summary>
    /// When crossing from Host bottom to Client top.
    /// </summary>
    public static NormalizedPoint MapHostBottomToClientTop(double hostX) =>
        new(Math.Clamp(hostX, 0, 1), 0);

    /// <summary>
    /// When crossing from Host top to Client bottom.
    /// </summary>
    public static NormalizedPoint MapHostTopToClientBottom(double hostX) =>
        new(Math.Clamp(hostX, 0, 1), 1);

    /// <summary>
    /// Map edge transition from one screen to the opposite edge of the other.
    /// </summary>
    public static NormalizedPoint MapEdgeTransition(Edge fromEdge, double coord)
    {
        return fromEdge switch
        {
            Edge.Right => MapHostRightToClientLeft(coord),
            Edge.Left => MapHostLeftToClientRight(coord),
            Edge.Bottom => MapHostBottomToClientTop(coord),
            Edge.Top => MapHostTopToClientBottom(coord),
            _ => new NormalizedPoint(0.5, 0.5)
        };
    }

    /// <summary>
    /// Map from client edge back to host.
    /// </summary>
    public static NormalizedPoint MapClientEdgeToHost(Edge fromEdge, double coord)
    {
        return fromEdge switch
        {
            Edge.Left => MapClientLeftToHostRight(coord),
            Edge.Right => MapClientRightToHostLeft(coord),
            Edge.Top => new NormalizedPoint(Math.Clamp(coord, 0, 1), 1),   // client top → host bottom
            Edge.Bottom => new NormalizedPoint(Math.Clamp(coord, 0, 1), 0), // client bottom → host top
            _ => new NormalizedPoint(0.5, 0.5)
        };
    }
}
