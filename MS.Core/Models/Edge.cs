namespace MS.Core.Models;

/// <summary>
/// Screen edge for transition detection (Host left ↔ Client right, etc.)
/// </summary>
[Flags]
public enum Edge
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8
}
