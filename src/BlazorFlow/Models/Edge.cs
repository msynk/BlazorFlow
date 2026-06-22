namespace BlazorFlow.Models;

/// <summary>
/// An edge connecting a source node (handle) to a target node (handle).
/// Mirrors React Flow's edge object.
/// </summary>
public class Edge
{
    /// <summary>Unique id. Required and must be unique across the flow.</summary>
    public required string Id { get; set; }

    /// <summary>Id of the source node.</summary>
    public required string Source { get; set; }

    /// <summary>Id of the target node.</summary>
    public required string Target { get; set; }

    /// <summary>Optional id of the specific source handle.</summary>
    public string? SourceHandle { get; set; }

    /// <summary>Optional id of the specific target handle.</summary>
    public string? TargetHandle { get; set; }

    /// <summary>Edge path algorithm. Defaults to <see cref="EdgeType.Bezier"/>.</summary>
    public EdgeType Type { get; set; } = EdgeType.Bezier;

    /// <summary>Optional label rendered at the edge's midpoint.</summary>
    public string? Label { get; set; }

    public bool Animated { get; set; }
    public bool Selected { get; set; }
    public bool Hidden { get; set; }

    /// <summary>Stroke width in pixels.</summary>
    public double StrokeWidth { get; set; } = 1.5;

    /// <summary>Stroke color (any CSS color). Falls back to the theme default when null.</summary>
    public string? Stroke { get; set; }

    /// <summary>Whether to draw an arrowhead marker at the target end.</summary>
    public bool MarkerEnd { get; set; } = true;

    /// <summary>Additional CSS class(es) applied to the edge group.</summary>
    public string? Class { get; set; }

    /// <summary>Arbitrary user payload.</summary>
    public object? Data { get; set; }
}

/// <summary>
/// A pending or completed connection between two handles, produced while the
/// user drags from a handle.
/// </summary>
public readonly record struct Connection(
    string? Source,
    string? Target,
    string? SourceHandle,
    string? TargetHandle);
