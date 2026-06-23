using BlazorFlow.Geometry;

namespace BlazorFlow.Models;

/// <summary>
/// Everything a custom edge template needs to render: the edge model, the computed
/// path/label geometry, and the resolved source/target anchors and sides. Mirrors the
/// props React Flow passes to a custom edge component.
/// </summary>
public sealed class EdgeContext
{
    public required Edge Edge { get; init; }
    public required PathResult Geometry { get; init; }

    public XYPosition Source { get; init; }
    public XYPosition Target { get; init; }
    public Position SourcePosition { get; init; }
    public Position TargetPosition { get; init; }

    public double SourceX => Source.X;
    public double SourceY => Source.Y;
    public double TargetX => Target.X;
    public double TargetY => Target.Y;
}
