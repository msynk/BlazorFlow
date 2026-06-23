namespace BlazorFlow.Models;

/// <summary>
/// Payload describing the current selection, raised by
/// <c>FlowCanvas.OnSelectionChange</c> whenever the selection changes.
/// </summary>
public readonly record struct SelectionChange(
    IReadOnlyList<Node> Nodes,
    IReadOnlyList<Edge> Edges);
