namespace BlazorFlow.Models;

/// <summary>
/// A node in the flow graph. This mirrors React Flow's node object.
/// Mutable so interactions (drag/select) can update it in place.
/// </summary>
public class Node
{
    /// <summary>Unique id. Required and must be unique across the flow.</summary>
    public required string Id { get; set; }

    /// <summary>Position in flow coordinate space (top-left of the node).</summary>
    public XYPosition Position { get; set; }

    /// <summary>
    /// Node type. Maps to a registered custom node renderer. "default", "input"
    /// and "output" are built in. Defaults to "default".
    /// </summary>
    public string Type { get; set; } = "default";

    /// <summary>Convenience label used by the built-in node renderers.</summary>
    public string? Label { get; set; }

    /// <summary>Arbitrary user payload available to custom node renderers.</summary>
    public object? Data { get; set; }

    /// <summary>Optional explicit width. When null the node is auto-sized and measured.</summary>
    public double? Width { get; set; }

    /// <summary>Optional explicit height. When null the node is auto-sized and measured.</summary>
    public double? Height { get; set; }

    public bool Selected { get; set; }
    public bool Draggable { get; set; } = true;
    public bool Selectable { get; set; } = true;
    public bool Connectable { get; set; } = true;
    public bool Hidden { get; set; }

    /// <summary>
    /// Optional id of a parent node. When set, <see cref="Position"/> is interpreted
    /// relative to the parent's top-left, enabling sub-flows / grouping.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>Additional CSS class(es) applied to the node element.</summary>
    public string? Class { get; set; }

    /// <summary>Inline style applied to the node element.</summary>
    public string? Style { get; set; }

    /// <summary>Optional z-index override.</summary>
    public int? ZIndex { get; set; }

    // ---- internal measured state (populated via JS measurement) ----

    /// <summary>Measured dimensions in flow units. Empty until first measurement.</summary>
    public Dimensions Measured { get; internal set; } = Dimensions.Empty;

    /// <summary>
    /// Absolute position in flow space, resolved by walking parent nodes.
    /// Maintained by the canvas each render; equals <see cref="Position"/> for root nodes.
    /// </summary>
    public XYPosition AbsolutePosition { get; internal set; }

    /// <summary>
    /// Measured handle anchors keyed by handle key (see <see cref="HandleBounds"/>),
    /// expressed as offsets from the node's top-left in flow units.
    /// </summary>
    public Dictionary<string, HandleBounds> Handles { get; } = new();

    public Dimensions EffectiveSize
    {
        get
        {
            var w = Width ?? (Measured.Width > 0 ? Measured.Width : DefaultWidth);
            var h = Height ?? (Measured.Height > 0 ? Measured.Height : DefaultHeight);
            return new Dimensions(w, h);
        }
    }

    public Rect GetRect()
    {
        var size = EffectiveSize;
        return new Rect(AbsolutePosition.X, AbsolutePosition.Y, size.Width, size.Height);
    }

    public const double DefaultWidth = 150;
    public const double DefaultHeight = 40;
}

/// <summary>
/// A measured handle anchor relative to its node's top-left, in flow units.
/// </summary>
public readonly record struct HandleBounds(
    string HandleId,
    HandleType Type,
    Position Position,
    double OffsetX,
    double OffsetY)
{
    /// <summary>Builds the dictionary key used to store/lookup a handle.</summary>
    public static string Key(HandleType type, string? handleId) =>
        $"{type}:{handleId ?? string.Empty}";
}
