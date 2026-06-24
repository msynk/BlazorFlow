namespace BlazorFlow.Models;

/// <summary>
/// Live state of an in-progress connection, passed to a custom connection-line template
/// and raised by <c>FlowCanvas.OnConnectionChange</c>. Mirrors the data React Flow exposes
/// through its connection line component / <c>useConnection</c> hook.
/// </summary>
public sealed class ConnectionLineContext
{
    /// <summary>The node the connection started from.</summary>
    public required string FromNode { get; init; }

    /// <summary>The handle id the connection started from (may be null).</summary>
    public string? FromHandle { get; init; }

    /// <summary>Whether the originating handle is a source or target.</summary>
    public HandleType FromHandleType { get; init; }

    /// <summary>The side the originating handle is anchored to.</summary>
    public Position FromPosition { get; init; }

    /// <summary>Start point in flow coordinates (the originating handle anchor).</summary>
    public XYPosition From { get; init; }

    /// <summary>Current pointer point in flow coordinates.</summary>
    public XYPosition To { get; init; }

    /// <summary>Computed end side used for path direction.</summary>
    public Position ToPosition { get; init; }

    /// <summary>True when the pointer is currently over a valid drop target.</summary>
    public bool IsValid { get; init; }
}

/// <summary>
/// Payload for <c>FlowCanvas.OnConnectEnd</c>, raised whenever a connection drag ends
/// regardless of whether it produced an edge. Mirrors React Flow's <c>onConnectEnd</c>
/// (event + final connection state), enabling "add node on edge drop" interactions.
/// </summary>
public readonly record struct ConnectEndInfo(
    ConnectionStartInfo Start,
    XYPosition FlowPosition,
    double ClientX,
    double ClientY,
    bool IsValid);
