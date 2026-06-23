namespace BlazorFlow.Models;

/// <summary>
/// A serializable snapshot of the flow state, returned by <c>FlowCanvas.ToObject()</c>.
/// Mirrors React Flow's <c>toObject()</c> result.
/// </summary>
public sealed record FlowSnapshot(
    IReadOnlyList<Node> Nodes,
    IReadOnlyList<Edge> Edges,
    Viewport Viewport);
