namespace MS.Core.Models;

/// <summary>
/// Where the Client screen is positioned relative to the Host screen.
/// Determines which edge triggers the cursor transition.
/// </summary>
public enum ClientPosition
{
    /// <summary>Client is to the right of Host. Host right edge → Client left edge.</summary>
    Right = 0,
    /// <summary>Client is to the left of Host. Host left edge → Client right edge.</summary>
    Left = 1,
    /// <summary>Client is above Host. Host top edge → Client bottom edge.</summary>
    Top = 2,
    /// <summary>Client is below Host. Host bottom edge → Client top edge.</summary>
    Bottom = 3
}
