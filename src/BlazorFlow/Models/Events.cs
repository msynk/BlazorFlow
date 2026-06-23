using Microsoft.AspNetCore.Components.Web;

namespace BlazorFlow.Models;

/// <summary>
/// Payload for node-related mouse events (click, double-click, context menu,
/// enter/leave). Carries both the node and the originating DOM event so callers
/// can read modifier keys and pointer coordinates (e.g. to position a context menu).
/// </summary>
public readonly record struct NodeMouseEvent(Node Node, MouseEventArgs Event);

/// <summary>
/// Payload for edge-related mouse events (double-click, context menu, enter/leave).
/// </summary>
public readonly record struct EdgeMouseEvent(Edge Edge, MouseEventArgs Event);

/// <summary>
/// Identifies the handle a connection drag started from. Raised by
/// <c>FlowCanvas.OnConnectStart</c>.
/// </summary>
public readonly record struct ConnectionStartInfo(
    string NodeId,
    string? HandleId,
    HandleType HandleType);

/// <summary>
/// Payload for <c>FlowCanvas.OnReconnect</c>, raised when an existing edge's
/// endpoint is dragged to a new handle. <see cref="Edge"/> already reflects the
/// updated source/target; <see cref="Connection"/> describes the new wiring.
/// </summary>
public readonly record struct EdgeReconnect(Edge Edge, Connection Connection);
