namespace BlazorFlow.Models;

/// <summary>
/// The pan/zoom transform applied to the flow's viewport layer.
/// Screen = Flow * Zoom + (X, Y).
/// </summary>
public readonly record struct Viewport(double X, double Y, double Zoom)
{
    public static readonly Viewport Identity = new(0, 0, 1);

    /// <summary>Converts a screen-space point (relative to the pane) to flow space.</summary>
    public XYPosition ScreenToFlow(double screenX, double screenY) =>
        new((screenX - X) / Zoom, (screenY - Y) / Zoom);

    /// <summary>Converts a flow-space point to screen space (relative to the pane).</summary>
    public XYPosition FlowToScreen(XYPosition flow) =>
        new(flow.X * Zoom + X, flow.Y * Zoom + Y);

    public string ToTransform() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"translate({X}px, {Y}px) scale({Zoom})");
}
