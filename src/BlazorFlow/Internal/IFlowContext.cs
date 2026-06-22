using BlazorFlow.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorFlow.Internal;

/// <summary>
/// Internal contract exposed by <c>FlowCanvas</c> to its descendant components
/// (handles, nodes, edges) via a cascading value.
/// </summary>
public interface IFlowContext
{
    Viewport Viewport { get; }

    /// <summary>True while the user is dragging a new connection from a handle.</summary>
    bool IsConnecting { get; }

    /// <summary>Begins a connection drag originating from the given handle.</summary>
    void StartConnection(string nodeId, string? handleId, HandleType handleType, Position position);

    /// <summary>Completes a connection drag when released over the given handle.</summary>
    void CompleteConnection(string nodeId, string? handleId, HandleType handleType);

    /// <summary>Marks the given handle as a hover candidate while connecting.</summary>
    void SetConnectionHover(string nodeId, string? handleId, HandleType handleType, bool isOver);

    /// <summary>Returns true when the given handle is a valid drop target for the in-progress connection.</summary>
    bool IsValidConnectionTarget(string nodeId, string? handleId, HandleType handleType);

    // ---- viewport controls (used by Controls / MiniMap) ----

    IReadOnlyList<Node> CurrentNodes { get; }

    /// <summary>Current size of the pane (the flow's outer container) in screen pixels.</summary>
    Dimensions PaneSize { get; }

    void ZoomIn();
    void ZoomOut();
    void FitView();
    void SetViewport(Viewport viewport);

    // ---- used by NodeResizer / NodeToolbar ----

    /// <summary>Requests a re-render of the canvas; optionally re-measures node/handle geometry.</summary>
    void Refresh(bool remeasure);

    /// <summary>Captures the pointer on the given element so a drag continues outside it.</summary>
    void CapturePointer(ElementReference element, long pointerId);

    /// <summary>Begins a node resize gesture (called by <c>NodeResizer</c>).</summary>
    void StartResize(string nodeId, ResizeDirection direction,
        double clientX, double clientY, double minWidth, double minHeight, bool keepAspectRatio);
}

/// <summary>
/// Cascaded down to the content of a single node so nested handles know which
/// node they belong to.
/// </summary>
public sealed class FlowNodeContext
{
    public required Node Node { get; init; }
}
