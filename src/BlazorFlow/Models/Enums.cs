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
/// Controls which handle pairings are allowed when connecting.
/// </summary>
public enum ConnectionMode
{
    /// <summary>Only source-to-target connections are valid (default).</summary>
    Strict,

    /// <summary>Any handle can connect to any other handle.</summary>
    Loose
}

/// <summary>
/// Color theme applied to the flow.
/// </summary>
public enum ColorMode
{
    Light,
    Dark,

    /// <summary>Follows the OS <c>prefers-color-scheme</c> setting.</summary>
    System
}

/// <summary>
/// How box selection decides whether a node is selected.
/// </summary>
public enum SelectionMode
{
    /// <summary>A node is selected if the selection box overlaps it at all.</summary>
    Partial,

    /// <summary>A node is selected only if it is fully enclosed by the selection box.</summary>
    Full
}

/// <summary>
/// Restricts the axis along which pan-on-scroll moves the viewport.
/// </summary>
public enum PanOnScrollMode
{
    Free,
    Horizontal,
    Vertical
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
