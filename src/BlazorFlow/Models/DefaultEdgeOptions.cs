namespace BlazorFlow.Models;

/// <summary>
/// Default values applied to edges created by user interaction (dragging/clicking
/// connections). Mirrors React Flow's <c>defaultEdgeOptions</c>.
/// </summary>
public sealed class DefaultEdgeOptions
{
    /// <summary>Edge path type. When null, the canvas's <c>DefaultEdgeType</c> is used.</summary>
    public EdgeType? Type { get; set; }

    public bool Animated { get; set; }

    /// <summary>Whether new edges draw a target arrowhead. When null, defaults to true.</summary>
    public bool? MarkerEnd { get; set; }

    public bool MarkerStart { get; set; }
    public MarkerType MarkerType { get; set; } = MarkerType.Arrow;

    public double? StrokeWidth { get; set; }
    public string? Stroke { get; set; }
    public bool Reconnectable { get; set; } = true;
    public string? Class { get; set; }

    /// <summary>Applies these options to a freshly created edge.</summary>
    public void ApplyTo(Edge edge, EdgeType fallbackType)
    {
        edge.Type = Type ?? fallbackType;
        edge.Animated = Animated;
        edge.MarkerEnd = MarkerEnd ?? true;
        edge.MarkerStart = MarkerStart;
        edge.MarkerType = MarkerType;
        if (StrokeWidth is { } sw) edge.StrokeWidth = sw;
        edge.Stroke = Stroke;
        edge.Reconnectable = Reconnectable;
        edge.Class = Class;
    }
}
