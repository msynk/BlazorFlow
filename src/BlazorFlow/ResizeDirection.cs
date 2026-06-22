namespace BlazorFlow;

/// <summary>
/// Which edges of a node a resize control manipulates. Corners combine two flags.
/// </summary>
[Flags]
public enum ResizeDirection
{
    None = 0,
    Top = 1,
    Right = 2,
    Bottom = 4,
    Left = 8,

    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomLeft = Bottom | Left,
    BottomRight = Bottom | Right,
}
