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
    /// Min distance from edge before we allow transition-back. Prevents immediate back-and-forth
    /// when cursor appears at edge after transition (and host warp to center).
    /// </summary>
    public const double LeaveEdgeThreshold = 0.05;

    /// <summary>
    /// True when cursor has moved away from the given edge (past LeaveEdgeThreshold).
    /// </summary>
    public static bool HasLeftEdge(double normalizedX, double normalizedY, Edge edge)
    {
        return edge switch
        {
            Edge.Left => normalizedX > LeaveEdgeThreshold,
            Edge.Right => normalizedX < 1 - LeaveEdgeThreshold,
            Edge.Top => normalizedY > LeaveEdgeThreshold,
            Edge.Bottom => normalizedY < 1 - LeaveEdgeThreshold,
            _ => true
        };
    }

    /// <summary>
    /// The Host edge that triggers transition to Client (the edge facing the client).
    /// </summary>
    public static Edge GetHostTransitionEdge(ClientPosition layout) => layout switch
    {
        ClientPosition.Right => Edge.Right,
        ClientPosition.Left => Edge.Left,
        ClientPosition.Top => Edge.Top,
        ClientPosition.Bottom => Edge.Bottom,
        _ => Edge.Right
    };

    /// <summary>
    /// The Client edge that triggers transition back to Host (the edge facing the host).
    /// </summary>
    public static Edge GetClientTransitionEdge(ClientPosition layout) => layout switch
    {
        ClientPosition.Right => Edge.Left,
        ClientPosition.Left => Edge.Right,
        ClientPosition.Top => Edge.Bottom,
        ClientPosition.Bottom => Edge.Top,
        _ => Edge.Left
    };

    /// <summary>
    /// Detects if the cursor is at the given edge (within threshold).
    /// </summary>
    public static bool IsAtEdge(double normalizedX, double normalizedY, Edge edge)
    {
        return edge switch
        {
            Edge.Left => normalizedX <= EdgeThreshold,
            Edge.Right => normalizedX >= 1 - EdgeThreshold,
            Edge.Top => normalizedY <= EdgeThreshold,
            Edge.Bottom => normalizedY >= 1 - EdgeThreshold,
            _ => false
        };
    }

    /// <summary>
    /// Detects if the cursor is at any screen edge.
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
    /// When transitioning from Host to Client: map Host edge + coord to initial Client position.
    /// </summary>
    public static NormalizedPoint MapHostEdgeToClient(Edge hostEdge, double coord)
    {
        return hostEdge switch
        {
            Edge.Right => MapHostRightToClientLeft(coord),
            Edge.Left => MapHostLeftToClientRight(coord),
            Edge.Bottom => MapHostBottomToClientTop(coord),
            Edge.Top => MapHostTopToClientBottom(coord),
            _ => new NormalizedPoint(0.5, 0.5)
        };
    }

    /// <summary>
    /// When transitioning from Client back to Host: map Client edge + coord to initial Host position.
    /// </summary>
    public static NormalizedPoint MapClientEdgeToHost(Edge clientEdge, double coord)
    {
        return clientEdge switch
        {
            Edge.Left => MapClientLeftToHostRight(coord),
            Edge.Right => MapClientRightToHostLeft(coord),
            Edge.Top => new NormalizedPoint(Math.Clamp(coord, 0, 1), 1),   // client top → host bottom
            Edge.Bottom => new NormalizedPoint(Math.Clamp(coord, 0, 1), 0), // client bottom → host top
            _ => new NormalizedPoint(0.5, 0.5)
        };
    }
}
