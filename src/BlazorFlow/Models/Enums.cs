namespace BlazorFlow.Models;

/// <summary>
/// The side of a node a handle is anchored to. Also used to drive edge path direction.
/// </summary>
public enum Position
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>
/// Whether a handle is a connection source or target (or can be both).
/// </summary>
public enum HandleType
{
    Source,
    Target
}

/// <summary>
/// Built-in edge path algorithms, mirroring React Flow's edge types.
/// </summary>
public enum EdgeType
{
    Bezier,
    Straight,
    Step,
    SmoothStep
}

/// <summary>
/// Keyboard modifier used to add to the current selection (multi-select).
/// </summary>
public enum ModifierKey
{
    Shift,
    Control,
    Alt,
    Meta
}

/// <summary>
/// Built-in background pattern variants.
/// </summary>
public enum BackgroundVariant
{
    Dots,
    Lines,
    Cross
}

/// <summary>
/// Arrowhead marker styles for edge ends, mirroring React Flow's MarkerType.
/// </summary>
public enum MarkerType
{
    /// <summary>An open (stroked) arrowhead.</summary>
    Arrow,

    /// <summary>A closed (filled) arrowhead.</summary>
    ArrowClosed
}
