using BlazorFlow.Geometry;
using BlazorFlow.Internal;
using BlazorFlow.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorFlow;

public partial class FlowCanvas : IFlowContext, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ---- public API ----

    /// <summary>The nodes to render. Mutated in place during drag/select.</summary>
    [Parameter, EditorRequired] public List<Node> Nodes { get; set; } = [];

    /// <summary>The edges to render.</summary>
    [Parameter] public List<Edge> Edges { get; set; } = [];

    /// <summary>Optional custom renderer for node content. Switch on <c>node.Type</c> inside.</summary>
    [Parameter] public RenderFragment<Node>? NodeTemplate { get; set; }

    /// <summary>
    /// Optional custom renderer for edges. Receives an <see cref="EdgeContext"/> with the
    /// computed geometry and endpoints; render SVG (path, labels, etc.). Switch on
    /// <c>context.Edge.Type</c> or custom data inside. When null, the built-in edge is drawn.
    /// </summary>
    [Parameter] public RenderFragment<EdgeContext>? EdgeTemplate { get; set; }

    /// <summary>
    /// Optional HTML renderer for edge labels, drawn in a non-transformed overlay so labels
    /// stay a constant screen size (React Flow's EdgeLabelRenderer). Positioned at the edge's
    /// label point. When set, it is used instead of the built-in SVG label.
    /// </summary>
    [Parameter] public RenderFragment<EdgeContext>? EdgeLabelTemplate { get; set; }

    /// <summary>Overlay content: place <c>Background</c>, <c>Controls</c>, <c>MiniMap</c>, <c>Panel</c> here.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public EventCallback<Connection> OnConnect { get; set; }

    /// <summary>Raised when the user begins dragging a connection from a handle.</summary>
    [Parameter] public EventCallback<ConnectionStartInfo> OnConnectStart { get; set; }

    /// <summary>Raised when a connection drag ends, whether it produced an edge or not.</summary>
    [Parameter] public EventCallback OnConnectEnd { get; set; }

    /// <summary>Globally enables dragging existing edge endpoints to new handles.</summary>
    [Parameter] public bool EdgesReconnectable { get; set; } = true;

    /// <summary>Raised when an existing edge is reconnected to a new handle.</summary>
    [Parameter] public EventCallback<EdgeReconnect> OnReconnect { get; set; }

    /// <summary>
    /// Optional predicate to validate a prospective connection while the user drags.
    /// Return false to reject; rejected targets are highlighted differently.
    /// </summary>
    [Parameter] public Func<Connection, bool>? IsValidConnection { get; set; }
    [Parameter] public EventCallback<List<Node>> NodesChanged { get; set; }
    [Parameter] public EventCallback<List<Edge>> EdgesChanged { get; set; }

    /// <summary>Raised with the nodes that were just deleted.</summary>
    [Parameter] public EventCallback<IReadOnlyList<Node>> OnNodesDelete { get; set; }

    /// <summary>Raised with the edges that were just deleted.</summary>
    [Parameter] public EventCallback<IReadOnlyList<Edge>> OnEdgesDelete { get; set; }

    /// <summary>Raised after any deletion with the removed nodes and edges.</summary>
    [Parameter] public EventCallback<SelectionChange> OnDelete { get; set; }
    [Parameter] public EventCallback<Node> OnNodeClick { get; set; }
    [Parameter] public EventCallback<Edge> OnEdgeClick { get; set; }
    [Parameter] public EventCallback<Viewport> OnViewportChanged { get; set; }

    // ---- richer event matrix (mirrors React Flow) ----

    [Parameter] public EventCallback<NodeMouseEvent> OnNodeDoubleClick { get; set; }
    [Parameter] public EventCallback<NodeMouseEvent> OnNodeContextMenu { get; set; }
    [Parameter] public EventCallback<NodeMouseEvent> OnNodeMouseEnter { get; set; }
    [Parameter] public EventCallback<NodeMouseEvent> OnNodeMouseLeave { get; set; }
    [Parameter] public EventCallback<Node> OnNodeDragStart { get; set; }
    [Parameter] public EventCallback<Node> OnNodeDrag { get; set; }
    [Parameter] public EventCallback<Node> OnNodeDragStop { get; set; }

    [Parameter] public EventCallback<EdgeMouseEvent> OnEdgeDoubleClick { get; set; }
    [Parameter] public EventCallback<EdgeMouseEvent> OnEdgeContextMenu { get; set; }
    [Parameter] public EventCallback<EdgeMouseEvent> OnEdgeMouseEnter { get; set; }
    [Parameter] public EventCallback<EdgeMouseEvent> OnEdgeMouseLeave { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnPaneClick { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnPaneContextMenu { get; set; }

    /// <summary>Raised whenever the set of selected nodes/edges changes.</summary>
    [Parameter] public EventCallback<SelectionChange> OnSelectionChange { get; set; }

    /// <summary>
    /// When true, dragging on the pane background draws a selection box instead of panning.
    /// Box selection is always available while holding Shift regardless of this setting.
    /// </summary>
    [Parameter] public bool SelectionOnDrag { get; set; }

    [Parameter] public double MinZoom { get; set; } = 0.2;
    [Parameter] public double MaxZoom { get; set; } = 2.5;

    /// <summary>
    /// Radius in screen pixels around the pointer within which a released connection
    /// snaps to the nearest valid handle. Set to 0 to require releasing exactly on a handle.
    /// </summary>
    [Parameter] public double ConnectionRadius { get; set; } = 20;

    /// <summary>
    /// When true, connections are made by clicking a source handle and then clicking a
    /// target handle, instead of (or in addition to) dragging.
    /// </summary>
    [Parameter] public bool ConnectOnClick { get; set; }

    /// <summary>Which modifier key adds to the current selection (multi-select). Defaults to Shift.</summary>
    [Parameter] public ModifierKey MultiSelectionKey { get; set; } = ModifierKey.Shift;
    [Parameter] public EdgeType DefaultEdgeType { get; set; } = EdgeType.Bezier;

    /// <summary>Default values applied to edges created by user interaction.</summary>
    [Parameter] public DefaultEdgeOptions? DefaultEdgeOptions { get; set; }

    /// <summary>Controls which handle pairings are valid when connecting (Strict vs Loose).</summary>
    [Parameter] public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Strict;

    /// <summary>Path style used for the in-progress connection line while dragging.</summary>
    [Parameter] public EdgeType ConnectionLineType { get; set; } = EdgeType.Bezier;

    /// <summary>Color theme applied to the flow.</summary>
    [Parameter] public ColorMode ColorMode { get; set; } = ColorMode.Light;

    /// <summary>
    /// Default origin (0,0 = top-left, 0.5,0.5 = center) defining which point of a node its
    /// Position refers to. Individual nodes can override via <c>Node.Origin</c>.
    /// </summary>
    [Parameter] public XYPosition NodeOrigin { get; set; } = new(0, 0);

    /// <summary>How box selection decides membership: Partial (any overlap) or Full (fully enclosed).</summary>
    [Parameter] public SelectionMode SelectionMode { get; set; } = SelectionMode.Partial;

    /// <summary>Restricts pan-on-scroll to a single axis when set.</summary>
    [Parameter] public PanOnScrollMode PanOnScrollMode { get; set; } = PanOnScrollMode.Free;

    /// <summary>Hides the small "BlazorFlow" attribution badge when true.</summary>
    [Parameter] public bool HideAttribution { get; set; }

    /// <summary>Raised once after the flow is first rendered and measured; receives the canvas for imperative use.</summary>
    [Parameter] public EventCallback<FlowCanvas> OnInit { get; set; }

    /// <summary>
    /// Optional veto for deletions. Receives the nodes and edges about to be deleted;
    /// return false to cancel the deletion. Mirrors React Flow's <c>onBeforeDelete</c>.
    /// </summary>
    [Parameter] public Func<IReadOnlyList<Node>, IReadOnlyList<Edge>, bool>? OnBeforeDelete { get; set; }
    [Parameter] public bool FitViewOnInit { get; set; }

    /// <summary>
    /// When true, only nodes/edges intersecting the current viewport (plus a margin) are rendered.
    /// Improves performance on large graphs. Unmeasured nodes are always rendered once so they can be sized.
    /// </summary>
    [Parameter] public bool OnlyRenderVisibleElements { get; set; }
    [Parameter] public bool PanOnDrag { get; set; } = true;
    [Parameter] public bool ZoomOnScroll { get; set; } = true;

    /// <summary>When true, mouse-wheel/trackpad scrolling pans the viewport; pinch (Ctrl+wheel) still zooms.</summary>
    [Parameter] public bool PanOnScroll { get; set; }

    /// <summary>Pan speed multiplier applied to wheel deltas when <see cref="PanOnScroll"/> is enabled.</summary>
    [Parameter] public double PanOnScrollSpeed { get; set; } = 0.5;

    /// <summary>When true, double-clicking the pane zooms in (Shift+double-click zooms out).</summary>
    [Parameter] public bool ZoomOnDoubleClick { get; set; } = true;
    [Parameter] public bool NodesDraggable { get; set; } = true;
    [Parameter] public bool ElementsSelectable { get; set; } = true;
    [Parameter] public bool DeleteKeyEnabled { get; set; } = true;

    /// <summary>Disables keyboard interactions (node focus + arrow-key movement) when true.</summary>
    [Parameter] public bool DisableKeyboardA11y { get; set; }

    /// <summary>When true, dragged node positions snap to <see cref="SnapGridX"/>/<see cref="SnapGridY"/>.</summary>
    [Parameter] public bool SnapToGrid { get; set; }

    /// <summary>Horizontal grid step (flow units) used when <see cref="SnapToGrid"/> is enabled.</summary>
    [Parameter] public double SnapGridX { get; set; } = 15;

    /// <summary>Vertical grid step (flow units) used when <see cref="SnapToGrid"/> is enabled.</summary>
    [Parameter] public double SnapGridY { get; set; } = 15;

    /// <summary>Optional flow-space rectangle that constrains how far the viewport can be panned.</summary>
    [Parameter] public Rect? TranslateExtent { get; set; }

    /// <summary>Optional flow-space rectangle that constrains where root nodes can be dragged.</summary>
    [Parameter] public Rect? NodeExtent { get; set; }

    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }

    // ---- state ----

    private ElementReference _paneRef;
    private ElementReference _viewportRef;
    private readonly string _markerPrefix = $"bf-arrow-{Guid.NewGuid():N}";

    private Viewport _viewport = Viewport.Identity;
    private DomRect? _paneRect;
    private FlowInterop _interop = default!;

    private bool _needsMeasure = true;

    // pan
    private bool _panning;
    private double _panStartX, _panStartY;
    private Viewport _panStartViewport;

    // box selection
    private bool _selecting;
    private XYPosition _selStart;
    private XYPosition _selEnd;
    private bool _selAdditive;
    private readonly HashSet<string> _preSelected = [];

    // viewport portals (ViewportPortal component content)
    private readonly Dictionary<object, RenderFragment> _viewportPortals = [];

    // node drag
    private bool _draggingNodes;
    private Node? _dragPrimary;
    private double _dragStartClientX, _dragStartClientY;
    private readonly List<(Node node, XYPosition start)> _dragSet = [];

    // connection
    private bool _connecting;
    private ConnectionSource _connSource;
    private XYPosition _connEnd;
    private (string nodeId, string? handleId, HandleType type)? _connHover;
    private Edge? _reconnectEdge;

    // resize
    private bool _resizing;
    private Node? _resizeNode;
    private ResizeDirection _resizeDir;
    private double _resizeStartClientX, _resizeStartClientY;
    private XYPosition _resizeStartPos;
    private Dimensions _resizeStartSize;
    private double _resizeMinW, _resizeMinH;
    private bool _resizeKeepAspect;

    private readonly record struct ConnectionSource(
        string NodeId, string? HandleId, HandleType Type, Models.Position Position, XYPosition Anchor);

    // ---- IFlowContext ----

    public Viewport Viewport => _viewport;
    public bool IsConnecting => _connecting;
    public IReadOnlyList<Node> CurrentNodes => Nodes;
    public Dimensions PaneSize => _paneRect is { } r ? new Dimensions(r.Width, r.Height) : Dimensions.Empty;

    // ---- lifecycle ----

    protected override void OnInitialized() => _interop = new FlowInterop(JS);

    protected override void OnParametersSet() => _needsMeasure = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RefreshPaneRectAsync();
        }

        if (_needsMeasure)
        {
            _needsMeasure = false;
            var changed = await MeasureAsync();
            if (firstRender && FitViewOnInit)
            {
                FitView();
                changed = true;
            }
            if (changed)
                StateHasChanged();
        }

        if (firstRender && OnInit.HasDelegate)
            await OnInit.InvokeAsync(this);
    }

    private async Task RefreshPaneRectAsync()
    {
        try { _paneRect = await _interop.GetRectAsync(new ElementReferenceLike(_paneRef)); }
        catch (JSDisconnectedException) { }
    }

    private async Task<bool> MeasureAsync()
    {
        MeasuredNode[] measured;
        try { measured = await _interop.MeasureNodesAsync(new ElementReferenceLike(_viewportRef), _viewport.Zoom); }
        catch (JSDisconnectedException) { return false; }

        bool changed = false;
        var byId = Nodes.ToDictionary(n => n.Id);
        foreach (var m in measured)
        {
            if (m.Id is null || !byId.TryGetValue(m.Id, out var node)) continue;

            var dims = new Dimensions(m.Width, m.Height);
            if (node.Measured != dims) { node.Measured = dims; changed = true; }

            node.Handles.Clear();
            foreach (var h in m.Handles)
            {
                var type = h.Type == "target" ? HandleType.Target : HandleType.Source;
                var pos = ParsePosition(h.Position);
                var key = HandleBounds.Key(type, string.IsNullOrEmpty(h.HandleId) ? null : h.HandleId);
                node.Handles[key] = new HandleBounds(h.HandleId, type, pos, h.OffsetX, h.OffsetY);
            }
            changed = true;
        }
        return changed;
    }

    // ---- rendering helpers ----

    private IEnumerable<Node> VisibleNodes()
    {
        ComputeLayout();
        var nodes = Nodes.Where(n => !n.Hidden);

        if (OnlyRenderVisibleElements && _paneRect is not null)
        {
            var view = VisibleFlowRect();
            // Always render not-yet-measured nodes so they can be sized at least once.
            nodes = nodes.Where(n => n.Measured.IsEmpty || RectsIntersect(n.GetRect(), view));
        }

        // Render shallower nodes first so group containers sit behind their children.
        return nodes.OrderBy(Depth);
    }

    private IEnumerable<Edge> VisibleEdges()
    {
        ComputeLayout();
        var edges = Edges.Where(e => !e.Hidden);

        if (OnlyRenderVisibleElements && _paneRect is not null)
        {
            var view = VisibleFlowRect();
            edges = edges.Where(e => EdgeIntersects(e, view));
        }

        return edges;
    }

    /// <summary>The currently visible region of flow space, expanded by a 20% margin on each side.</summary>
    private Rect VisibleFlowRect()
    {
        var topLeft = _viewport.ScreenToFlow(0, 0);
        var w = _paneRect!.Width / _viewport.Zoom;
        var h = _paneRect.Height / _viewport.Zoom;
        var mx = w * 0.2;
        var my = h * 0.2;
        return new Rect(topLeft.X - mx, topLeft.Y - my, w + 2 * mx, h + 2 * my);
    }

    private bool EdgeIntersects(Edge edge, Rect view)
    {
        var (s, t) = GetEdgeEndpoints(edge);
        var minX = Math.Min(s.X, t.X);
        var minY = Math.Min(s.Y, t.Y);
        var rect = new Rect(minX, minY, Math.Abs(t.X - s.X) + 1, Math.Abs(t.Y - s.Y) + 1);
        return RectsIntersect(rect, view);
    }

    /// <summary>Resolves each node's absolute position by walking its parent chain, then applies node origin.</summary>
    private void ComputeLayout()
    {
        var index = Nodes.ToDictionary(n => n.Id);
        foreach (var n in Nodes)
        {
            var raw = ResolveAbsolute(n, index, 0);
            var origin = n.Origin ?? NodeOrigin;
            if (origin.X == 0 && origin.Y == 0)
            {
                n.AbsolutePosition = raw;
            }
            else
            {
                var size = n.EffectiveSize;
                n.AbsolutePosition = new XYPosition(raw.X - origin.X * size.Width, raw.Y - origin.Y * size.Height);
            }
        }
    }

    private static XYPosition ResolveAbsolute(Node n, Dictionary<string, Node> index, int depth)
    {
        if (depth < 50 && n.ParentId is { } pid && index.TryGetValue(pid, out var parent))
        {
            var pa = ResolveAbsolute(parent, index, depth + 1);
            return new XYPosition(pa.X + n.Position.X, pa.Y + n.Position.Y);
        }
        return n.Position;
    }

    private int Depth(Node n)
    {
        var index = Nodes.ToDictionary(x => x.Id);
        int d = 0;
        var current = n;
        while (d < 50 && current.ParentId is { } pid && index.TryGetValue(pid, out var parent))
        {
            d++;
            current = parent;
        }
        return d;
    }

    private bool HasAncestorIn(Node node, HashSet<string> ids)
    {
        var index = Nodes.ToDictionary(x => x.Id);
        var current = node;
        int guard = 0;
        while (guard++ < 50 && current.ParentId is { } pid && index.TryGetValue(pid, out var parent))
        {
            if (ids.Contains(pid)) return true;
            current = parent;
        }
        return false;
    }

    private EdgeContext GetEdgeContext(Edge edge)
    {
        var source = Nodes.FirstOrDefault(n => n.Id == edge.Source);
        var target = Nodes.FirstOrDefault(n => n.Id == edge.Target);
        if (source is null || target is null)
        {
            return new EdgeContext
            {
                Edge = edge,
                Geometry = new PathResult(string.Empty, 0, 0),
            };
        }

        var (sx, sy, sPos) = ResolveAnchor(source, HandleType.Source, edge.SourceHandle, Models.Position.Bottom);
        var (tx, ty, tPos) = ResolveAnchor(target, HandleType.Target, edge.TargetHandle, Models.Position.Top);

        return new EdgeContext
        {
            Edge = edge,
            Geometry = EdgePath.Compute(edge.Type, sx, sy, sPos, tx, ty, tPos),
            Source = new XYPosition(sx, sy),
            Target = new XYPosition(tx, ty),
            SourcePosition = sPos,
            TargetPosition = tPos,
        };
    }

    private (double x, double y, Models.Position pos) ResolveAnchor(
        Node node, HandleType type, string? handleId, Models.Position fallback)
    {
        var key = HandleBounds.Key(type, handleId);
        if (node.Handles.TryGetValue(key, out var h))
            return (node.AbsolutePosition.X + h.OffsetX, node.AbsolutePosition.Y + h.OffsetY, h.Position);

        // No measured handle: anchor on the fallback side at the node's edge midpoint.
        var size = node.EffectiveSize;
        var p = node.AbsolutePosition;
        return fallback switch
        {
            Models.Position.Top => (p.X + size.Width / 2, p.Y, fallback),
            Models.Position.Bottom => (p.X + size.Width / 2, p.Y + size.Height, fallback),
            Models.Position.Left => (p.X, p.Y + size.Height / 2, fallback),
            _ => (p.X + size.Width, p.Y + size.Height / 2, fallback),
        };
    }

    private string ConnectionLinePath()
    {
        var s = _connSource;
        var endPos = OppositePosition(s.Position);
        var result = EdgePath.Compute(ConnectionLineType, s.Anchor.X, s.Anchor.Y, s.Position, _connEnd.X, _connEnd.Y, endPos);
        return result.Path;
    }

    // ---- pane interactions ----

    /// <summary>True when the configured multi-selection modifier is held.</summary>
    private bool IsMultiSelect(MouseEventArgs e) => MultiSelectionKey switch
    {
        ModifierKey.Control => e.CtrlKey,
        ModifierKey.Alt => e.AltKey,
        ModifierKey.Meta => e.MetaKey,
        _ => e.ShiftKey,
    };

    private async Task OnPanePointerDown(PointerEventArgs e)
    {
        // Reaching here means the pointer hit the pane background (nodes/handles stop propagation).
        if (e.Button != 0) return;

        // A pending click-connection is cancelled by clicking empty space.
        if (_connecting)
        {
            CancelConnection();
            return;
        }

        var multi = IsMultiSelect(e);

        // Box selection: multi-select-drag always, or plain drag when SelectionOnDrag is enabled.
        if (ElementsSelectable && (multi || SelectionOnDrag))
        {
            await RefreshPaneRectAsync();
            _selAdditive = multi;
            _preSelected.Clear();
            if (_selAdditive)
            {
                foreach (var n in Nodes)
                    if (n.Selected) _preSelected.Add(n.Id);
            }
            else
            {
                ClearSelection();
                await NotifySelectionChangedAsync();
            }

            _selecting = true;
            _selStart = ScreenToFlow(e);
            _selEnd = _selStart;
            StateHasChanged();
            return;
        }

        if (ElementsSelectable)
        {
            ClearSelection();
            await NotifySelectionChangedAsync();
        }

        if (PanOnDrag)
        {
            await RefreshPaneRectAsync();
            _panning = true;
            _panStartX = e.ClientX;
            _panStartY = e.ClientY;
            _panStartViewport = _viewport;
        }
    }

    private void OnPanePointerMove(PointerEventArgs e)
    {
        if (_panning)
        {
            _viewport = ClampViewport(_panStartViewport with
            {
                X = _panStartViewport.X + (e.ClientX - _panStartX),
                Y = _panStartViewport.Y + (e.ClientY - _panStartY),
            });
            StateHasChanged();
        }
        else if (_draggingNodes)
        {
            var dx = (e.ClientX - _dragStartClientX) / _viewport.Zoom;
            var dy = (e.ClientY - _dragStartClientY) / _viewport.Zoom;
            foreach (var (node, start) in _dragSet)
                node.Position = ClampNodePosition(node, Snap(new XYPosition(start.X + dx, start.Y + dy)));
            if (_dragPrimary is not null)
                _ = OnNodeDrag.InvokeAsync(_dragPrimary);
            StateHasChanged();
        }
        else if (_connecting)
        {
            _connEnd = ScreenToFlow(e);
            StateHasChanged();
        }
        else if (_selecting)
        {
            _selEnd = ScreenToFlow(e);
            ApplyBoxSelection();
            StateHasChanged();
        }
        else if (_resizing && _resizeNode is not null)
        {
            ApplyResize(e);
            StateHasChanged();
        }
    }

    private async Task OnPanePointerUp(PointerEventArgs e)
    {
        if (_panning)
        {
            _panning = false;
            await OnViewportChanged.InvokeAsync(_viewport);
        }

        if (_draggingNodes)
        {
            _draggingNodes = false;
            var dragged = _dragPrimary;
            _dragPrimary = null;
            _dragSet.Clear();
            _needsMeasure = true; // node moved: re-measure handle anchors
            if (dragged is not null)
                await OnNodeDragStop.InvokeAsync(dragged);
            await NodesChanged.InvokeAsync(Nodes);
        }

        if (_connecting)
        {
            // Released over empty space => try to snap to a nearby handle, else cancel.
            if (!TryCompleteByProximity())
                CancelConnection();
        }

        if (_selecting)
        {
            _selecting = false;
            _preSelected.Clear();
            await NotifySelectionChangedAsync();
            StateHasChanged();
        }

        if (_resizing)
        {
            _resizing = false;
            _resizeNode = null;
            _needsMeasure = true; // size changed: re-measure handle anchors
            await NodesChanged.InvokeAsync(Nodes);
            StateHasChanged();
        }
    }

    private async Task OnWheel(WheelEventArgs e)
    {
        if (_paneRect is null) await RefreshPaneRectAsync();

        // Pinch (Ctrl/Cmd+wheel) always zooms; otherwise PanOnScroll decides pan vs zoom.
        var zoomGesture = e.CtrlKey || !PanOnScroll;

        if (zoomGesture)
        {
            if (!ZoomOnScroll && !e.CtrlKey) return;
            var factor = e.DeltaY < 0 ? 1.15 : 1 / 1.15;
            var px = e.ClientX - (_paneRect?.Left ?? 0);
            var py = e.ClientY - (_paneRect?.Top ?? 0);
            ZoomAtScreenPoint(px, py, factor);
            return;
        }

        // Pan: vertical scroll moves Y; Shift swaps to horizontal.
        var dx = e.ShiftKey ? e.DeltaY : e.DeltaX;
        var dy = e.ShiftKey ? 0 : e.DeltaY;
        if (PanOnScrollMode == PanOnScrollMode.Horizontal) { dx = e.DeltaY; dy = 0; }
        else if (PanOnScrollMode == PanOnScrollMode.Vertical) { dx = 0; dy = e.DeltaY; }
        _viewport = ClampViewport(_viewport with
        {
            X = _viewport.X - dx * PanOnScrollSpeed,
            Y = _viewport.Y - dy * PanOnScrollSpeed,
        });
        await OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    private void ZoomAtScreenPoint(double px, double py, double factor)
    {
        var newZoom = Math.Clamp(_viewport.Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _viewport.Zoom) < 1e-6) return;

        // Keep the flow point under the cursor stationary.
        var flowX = (px - _viewport.X) / _viewport.Zoom;
        var flowY = (py - _viewport.Y) / _viewport.Zoom;
        _viewport = ClampViewport(new Viewport(px - flowX * newZoom, py - flowY * newZoom, newZoom));
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    private async Task OnPaneDoubleClick(MouseEventArgs e)
    {
        if (!ZoomOnDoubleClick) return;
        if (_paneRect is null) await RefreshPaneRectAsync();
        var px = e.ClientX - (_paneRect?.Left ?? 0);
        var py = e.ClientY - (_paneRect?.Top ?? 0);
        ZoomAtScreenPoint(px, py, e.ShiftKey ? 1 / 1.2 : 1.2);
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (DeleteKeyEnabled && e.Key is "Delete" or "Backspace")
        {
            await DeleteSelectionAsync();
            return;
        }

        if (!DisableKeyboardA11y && e.Key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown")
            await MoveSelectedNodesAsync(e);
    }

    private async Task MoveSelectedNodesAsync(KeyboardEventArgs e)
    {
        var step = e.ShiftKey ? 25 : (SnapToGrid ? Math.Max(SnapGridX, SnapGridY) : 5);
        var (dx, dy) = e.Key switch
        {
            "ArrowLeft" => (-step, 0.0),
            "ArrowRight" => (step, 0.0),
            "ArrowUp" => (0.0, -step),
            "ArrowDown" => (0.0, step),
            _ => (0.0, 0.0),
        };

        var moved = false;
        foreach (var n in Nodes)
        {
            if (!n.Selected || !n.Draggable) continue;
            n.Position = ClampNodePosition(n, new XYPosition(n.Position.X + dx, n.Position.Y + dy));
            moved = true;
        }

        if (moved)
        {
            _needsMeasure = true;
            await NodesChanged.InvokeAsync(Nodes);
            StateHasChanged();
        }
    }

    private Task OnPaneClickHandler(MouseEventArgs e) => OnPaneClick.InvokeAsync(e);
    private Task OnPaneContextMenuHandler(MouseEventArgs e) => OnPaneContextMenu.InvokeAsync(e);

    // ---- node drag / selection ----

    private void StartNodeDrag(Node node, PointerEventArgs e)
    {
        SelectOnPointerDown(node, e);
        _ = OnNodeClick.InvokeAsync(node);

        // When the node uses a custom drag handle, the body selects but doesn't drag.
        if (node.UseCustomDragHandle) return;

        BeginDrag(node, e);
    }

    /// <summary>Starts dragging a node, bypassing the custom-drag-handle restriction. Called by <c>DragHandle</c>.</summary>
    public void BeginNodeDrag(Node node, PointerEventArgs e)
    {
        SelectOnPointerDown(node, e);
        _ = OnNodeClick.InvokeAsync(node);
        BeginDrag(node, e);
    }

    private void SelectOnPointerDown(Node node, PointerEventArgs e)
    {
        if (!ElementsSelectable) return;
        if (!node.Selected && !IsMultiSelect(e)) ClearSelection();
        node.Selected = true;
        _ = NotifySelectionChangedAsync();
    }

    private void BeginDrag(Node node, PointerEventArgs e)
    {
        if (!NodesDraggable || !node.Draggable) return;

        _draggingNodes = true;
        _dragPrimary = node;
        _dragStartClientX = e.ClientX;
        _dragStartClientY = e.ClientY;
        _dragSet.Clear();
        var moving = Nodes.Where(n => n.Selected && n.Draggable).ToList();
        if (moving.Count == 0) moving.Add(node);

        // Exclude nodes whose ancestor is also moving: a parent drag already carries its children.
        var movingIds = moving.Select(n => n.Id).ToHashSet();
        foreach (var n in moving)
            if (!HasAncestorIn(n, movingIds))
                _dragSet.Add((n, n.Position));

        _ = OnNodeDragStart.InvokeAsync(node);
    }

    private void SelectEdge(Edge edge, PointerEventArgs e)
    {
        if (ElementsSelectable)
        {
            if (!IsMultiSelect(e)) ClearSelection();
            edge.Selected = true;
            _ = NotifySelectionChangedAsync();
        }
        _ = OnEdgeClick.InvokeAsync(edge);
        StateHasChanged();
    }

    private void ClearSelection()
    {
        foreach (var n in Nodes) n.Selected = false;
        foreach (var ed in Edges) ed.Selected = false;
    }

    private void ApplyBoxSelection()
    {
        var rect = SelectionRect();
        var partial = SelectionMode == SelectionMode.Partial;
        foreach (var n in Nodes)
        {
            if (n.Hidden || !n.Selectable) continue;
            var inside = NodeIntersects(n.GetRect(), rect, partial);
            n.Selected = inside || (_selAdditive && _preSelected.Contains(n.Id));
        }
    }

    private Rect SelectionRect()
    {
        var x = Math.Min(_selStart.X, _selEnd.X);
        var y = Math.Min(_selStart.Y, _selEnd.Y);
        var w = Math.Abs(_selEnd.X - _selStart.X);
        var h = Math.Abs(_selEnd.Y - _selStart.Y);
        return new Rect(x, y, w, h);
    }

    private static bool RectsIntersect(Rect a, Rect b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private string SelectionStyle() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"transform:translate({SelectionRect().X}px,{SelectionRect().Y}px);width:{SelectionRect().Width}px;height:{SelectionRect().Height}px;");

    private Task NotifySelectionChangedAsync()
    {
        if (!OnSelectionChange.HasDelegate) return Task.CompletedTask;
        var nodes = Nodes.Where(n => n.Selected).ToList();
        var edges = Edges.Where(ed => ed.Selected).ToList();
        return OnSelectionChange.InvokeAsync(new SelectionChange(nodes, edges));
    }

    private async Task DeleteSelectionAsync()
    {
        var nodesToDelete = Nodes.Where(n => n.Selected).ToList();
        var edgesToDelete = Edges.Where(ed => ed.Selected).ToList();
        if (nodesToDelete.Count == 0 && edgesToDelete.Count == 0) return;

        if (OnBeforeDelete is not null && !OnBeforeDelete(nodesToDelete, edgesToDelete))
            return;

        var removedNodes = nodesToDelete.Select(n => n.Id).ToHashSet();
        // Edges removed = explicitly selected edges plus those attached to removed nodes.
        var removedEdges = Edges
            .Where(ed => ed.Selected || removedNodes.Contains(ed.Source) || removedNodes.Contains(ed.Target))
            .ToList();

        if (removedNodes.Count > 0)
        {
            Nodes.RemoveAll(n => removedNodes.Contains(n.Id));
            Edges.RemoveAll(ed => removedNodes.Contains(ed.Source) || removedNodes.Contains(ed.Target));
        }
        Edges.RemoveAll(ed => ed.Selected);

        await NotifySelectionChangedAsync();
        await RaiseDeleteEventsAsync(nodesToDelete, removedEdges);
        await NodesChanged.InvokeAsync(Nodes);
        await EdgesChanged.InvokeAsync(Edges);
        StateHasChanged();
    }

    private async Task RaiseDeleteEventsAsync(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        if (nodes.Count > 0 && OnNodesDelete.HasDelegate) await OnNodesDelete.InvokeAsync(nodes);
        if (edges.Count > 0 && OnEdgesDelete.HasDelegate) await OnEdgesDelete.InvokeAsync(edges);
        if ((nodes.Count > 0 || edges.Count > 0) && OnDelete.HasDelegate)
            await OnDelete.InvokeAsync(new SelectionChange(nodes, edges));
    }

    // ---- connection handling (called by Handle via IFlowContext) ----

    public void StartConnection(string nodeId, string? handleId, HandleType handleType, Models.Position position)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        var (x, y, pos) = ResolveAnchor(node, handleType, handleId, position);
        _connSource = new ConnectionSource(nodeId, handleId, handleType, pos, new XYPosition(x, y));
        _connEnd = new XYPosition(x, y);
        _connecting = true;
        _ = OnConnectStart.InvokeAsync(new ConnectionStartInfo(nodeId, handleId, handleType));
        _ = RefreshPaneRectAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Begins reconnecting one end of an existing edge. The opposite (fixed) end becomes
    /// the connection source, so dragging behaves like drawing a new connection from it.
    /// </summary>
    private void StartReconnect(Edge edge, HandleType movingEnd, PointerEventArgs e)
    {
        if (e.Button != 0 || !EdgesReconnectable || !edge.Reconnectable) return;

        var fixedType = movingEnd == HandleType.Target ? HandleType.Source : HandleType.Target;
        var fixedNodeId = fixedType == HandleType.Source ? edge.Source : edge.Target;
        var fixedHandle = fixedType == HandleType.Source ? edge.SourceHandle : edge.TargetHandle;
        var fallback = fixedType == HandleType.Source ? Models.Position.Bottom : Models.Position.Top;

        var node = Nodes.FirstOrDefault(n => n.Id == fixedNodeId);
        if (node is null) return;

        var (x, y, pos) = ResolveAnchor(node, fixedType, fixedHandle, fallback);
        _connSource = new ConnectionSource(node.Id, fixedHandle, fixedType, pos, new XYPosition(x, y));
        _connEnd = ScreenToFlow(e);
        _connecting = true;
        _reconnectEdge = edge;
        _ = OnConnectStart.InvokeAsync(new ConnectionStartInfo(node.Id, fixedHandle, fixedType));
        _ = RefreshPaneRectAsync();
        StateHasChanged();
    }

    /// <summary>Resolves the source and target endpoint anchors of an edge in flow space.</summary>
    private (XYPosition Source, XYPosition Target) GetEdgeEndpoints(Edge edge)
    {
        var s = Nodes.FirstOrDefault(n => n.Id == edge.Source);
        var t = Nodes.FirstOrDefault(n => n.Id == edge.Target);
        if (s is null || t is null) return (default, default);
        var (sx, sy, _) = ResolveAnchor(s, HandleType.Source, edge.SourceHandle, Models.Position.Bottom);
        var (tx, ty, _) = ResolveAnchor(t, HandleType.Target, edge.TargetHandle, Models.Position.Top);
        return (new XYPosition(sx, sy), new XYPosition(tx, ty));
    }

    public void CompleteConnection(string nodeId, string? handleId, HandleType handleType)
    {
        if (!_connecting) return;

        if (IsValidConnectionTarget(nodeId, handleId, handleType))
        {
            // Normalize so the edge always flows source-handle -> target-handle.
            var (sourceNode, sourceHandle, targetNode, targetHandle) =
                _connSource.Type == HandleType.Source
                    ? (_connSource.NodeId, _connSource.HandleId, nodeId, handleId)
                    : (nodeId, handleId, _connSource.NodeId, _connSource.HandleId);

            if (_reconnectEdge is { } re)
            {
                // Reconnecting an existing edge: rewire it in place.
                re.Source = sourceNode;
                re.SourceHandle = sourceHandle;
                re.Target = targetNode;
                re.TargetHandle = targetHandle;
                var conn = new Connection(sourceNode, targetNode, sourceHandle, targetHandle);
                _needsMeasure = true;
                _ = OnReconnect.InvokeAsync(new EdgeReconnect(re, conn));
                _ = EdgesChanged.InvokeAsync(Edges);
            }
            else
            {
                var connection = new Connection(sourceNode, targetNode, sourceHandle, targetHandle);
                var edge = new Edge
                {
                    Id = $"e-{sourceNode}{sourceHandle}-{targetNode}{targetHandle}-{Guid.NewGuid():N}".Replace(" ", ""),
                    Source = sourceNode,
                    Target = targetNode,
                    SourceHandle = sourceHandle,
                    TargetHandle = targetHandle,
                    Type = DefaultEdgeType,
                };
                DefaultEdgeOptions?.ApplyTo(edge, DefaultEdgeType);
                Edges.Add(edge);
                _ = OnConnect.InvokeAsync(connection);
                _ = EdgesChanged.InvokeAsync(Edges);
            }
        }

        CancelConnection();
    }

    private bool TryCompleteByProximity()
    {
        if (ConnectionRadius <= 0) return false;
        var radius = ConnectionRadius / _viewport.Zoom;

        double best = double.MaxValue;
        (string nodeId, string? handleId, HandleType type)? bestHandle = null;

        foreach (var n in Nodes)
        {
            if (n.Hidden || !n.Connectable) continue;
            foreach (var (_, h) in n.Handles)
            {
                var hx = n.AbsolutePosition.X + h.OffsetX;
                var hy = n.AbsolutePosition.Y + h.OffsetY;
                var dx = hx - _connEnd.X;
                var dy = hy - _connEnd.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d > radius || d >= best) continue;

                var hid = string.IsNullOrEmpty(h.HandleId) ? null : h.HandleId;
                if (!IsValidConnectionTarget(n.Id, hid, h.Type)) continue;

                best = d;
                bestHandle = (n.Id, hid, h.Type);
            }
        }

        if (bestHandle is { } bh)
        {
            CompleteConnection(bh.nodeId, bh.handleId, bh.type);
            return true;
        }
        return false;
    }

    private void CancelConnection()
    {
        _connecting = false;
        _connHover = null;
        _reconnectEdge = null;
        _ = OnConnectEnd.InvokeAsync();
        StateHasChanged();
    }

    public void SetConnectionHover(string nodeId, string? handleId, HandleType handleType, bool isOver)
    {
        if (!_connecting) return;
        _connHover = isOver ? (nodeId, handleId, handleType) : null;
    }

    public bool IsValidConnectionTarget(string nodeId, string? handleId, HandleType handleType)
    {
        if (!_connecting) return false;
        if (ConnectionMode == ConnectionMode.Strict && handleType == _connSource.Type) return false; // strict: source<->target only
        if (nodeId == _connSource.NodeId) return false;            // no self-connection

        if (IsValidConnection is not null)
        {
            var (sourceNode, sourceHandle, targetNode, targetHandle) =
                _connSource.Type == HandleType.Source
                    ? (_connSource.NodeId, _connSource.HandleId, nodeId, handleId)
                    : (nodeId, handleId, _connSource.NodeId, _connSource.HandleId);
            if (!IsValidConnection(new Connection(sourceNode, targetNode, sourceHandle, targetHandle)))
                return false;
        }

        return true;
    }

    // ---- viewport controls ----

    public void ZoomIn() => ZoomBy(1.2);
    public void ZoomOut() => ZoomBy(1 / 1.2);

    private void ZoomBy(double factor)
    {
        var newZoom = Math.Clamp(_viewport.Zoom * factor, MinZoom, MaxZoom);
        var cx = (_paneRect?.Width ?? 0) / 2;
        var cy = (_paneRect?.Height ?? 0) / 2;
        var flowX = (cx - _viewport.X) / _viewport.Zoom;
        var flowY = (cy - _viewport.Y) / _viewport.Zoom;
        _viewport = new Viewport(cx - flowX * newZoom, cy - flowY * newZoom, newZoom);
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    public void SetViewport(Viewport viewport)
    {
        _viewport = viewport;
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    // ---- public imperative API (the useReactFlow() equivalent, accessed via @ref) ----

    /// <summary>The current viewport pan/zoom transform.</summary>
    public Viewport GetViewport() => _viewport;

    /// <summary>Converts a screen-space (client) point to flow coordinates.</summary>
    public XYPosition ScreenToFlowPosition(double clientX, double clientY)
    {
        var px = clientX - (_paneRect?.Left ?? 0);
        var py = clientY - (_paneRect?.Top ?? 0);
        return _viewport.ScreenToFlow(px, py);
    }

    /// <summary>Converts a flow-space point to a screen-space (client) point.</summary>
    public XYPosition FlowToScreenPosition(XYPosition flow)
    {
        var p = _viewport.FlowToScreen(flow);
        return new XYPosition(p.X + (_paneRect?.Left ?? 0), p.Y + (_paneRect?.Top ?? 0));
    }

    /// <summary>Sets the absolute zoom level, keeping the pane center stationary.</summary>
    public void ZoomTo(double zoom)
    {
        var newZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        var cx = (_paneRect?.Width ?? 0) / 2;
        var cy = (_paneRect?.Height ?? 0) / 2;
        var flowX = (cx - _viewport.X) / _viewport.Zoom;
        var flowY = (cy - _viewport.Y) / _viewport.Zoom;
        _viewport = new Viewport(cx - flowX * newZoom, cy - flowY * newZoom, newZoom);
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    /// <summary>Centers the viewport on the given flow point, optionally setting the zoom.</summary>
    public void SetCenter(double x, double y, double? zoom = null)
    {
        var z = Math.Clamp(zoom ?? _viewport.Zoom, MinZoom, MaxZoom);
        var paneW = _paneRect?.Width ?? 0;
        var paneH = _paneRect?.Height ?? 0;
        _viewport = new Viewport(paneW / 2 - x * z, paneH / 2 - y * z, z);
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    /// <summary>Pans/zooms so the given flow-space rectangle fills the pane.</summary>
    public void FitBounds(Rect bounds, double padding = 0.1)
    {
        if (_paneRect is null) return;
        var w = Math.Max(1, bounds.Width);
        var h = Math.Max(1, bounds.Height);
        var paneW = _paneRect.Width;
        var paneH = _paneRect.Height;
        var zoom = Math.Clamp(Math.Min(paneW / w, paneH / h) * (1 - padding), MinZoom, MaxZoom);
        _viewport = new Viewport(paneW / 2 - (bounds.X + w / 2) * zoom, paneH / 2 - (bounds.Y + h / 2) * zoom, zoom);
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    /// <summary>Returns the node with the given id, or null.</summary>
    public Node? GetNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Returns the edge with the given id, or null.</summary>
    public Edge? GetEdge(string id) => Edges.FirstOrDefault(e => e.Id == id);

    /// <summary>Appends nodes and triggers a re-measure.</summary>
    public void AddNodes(params Node[] nodes)
    {
        if (nodes.Length == 0) return;
        Nodes.AddRange(nodes);
        _needsMeasure = true;
        _ = NodesChanged.InvokeAsync(Nodes);
        StateHasChanged();
    }

    /// <summary>Appends edges.</summary>
    public void AddEdges(params Edge[] edges)
    {
        if (edges.Length == 0) return;
        Edges.AddRange(edges);
        _ = EdgesChanged.InvokeAsync(Edges);
        StateHasChanged();
    }

    /// <summary>Removes the given nodes (and their connected edges) and/or edges.</summary>
    public async Task DeleteElementsAsync(IEnumerable<Node>? nodes = null, IEnumerable<Edge>? edges = null)
    {
        var nodeList = nodes?.ToList() ?? [];
        var edgeList = edges?.ToList() ?? [];

        if (OnBeforeDelete is not null && !OnBeforeDelete(nodeList, edgeList))
            return;

        var nodeIds = nodeList.Select(n => n.Id).ToHashSet();
        var removedEdges = edgeList.ToList();
        if (nodeIds.Count > 0)
        {
            removedEdges.AddRange(Edges.Where(e =>
                (nodeIds.Contains(e.Source) || nodeIds.Contains(e.Target)) && !removedEdges.Contains(e)));
            Nodes.RemoveAll(n => nodeIds.Contains(n.Id));
            Edges.RemoveAll(e => nodeIds.Contains(e.Source) || nodeIds.Contains(e.Target));
        }
        if (edgeList.Count > 0)
        {
            var edgeIds = edgeList.Select(e => e.Id).ToHashSet();
            Edges.RemoveAll(e => edgeIds.Contains(e.Id));
        }
        await RaiseDeleteEventsAsync(nodeList, removedEdges);
        await NodesChanged.InvokeAsync(Nodes);
        await EdgesChanged.InvokeAsync(Edges);
        StateHasChanged();
    }

    /// <summary>Returns the nodes overlapping the given flow-space rectangle.</summary>
    public IReadOnlyList<Node> GetIntersectingNodes(Rect area, bool partially = true)
    {
        var list = new List<Node>();
        foreach (var n in Nodes)
        {
            if (n.Hidden) continue;
            if (NodeIntersects(n.GetRect(), area, partially)) list.Add(n);
        }
        return list;
    }

    /// <summary>Returns the nodes overlapping the given node's bounds (excluding itself).</summary>
    public IReadOnlyList<Node> GetIntersectingNodes(Node node, bool partially = true)
    {
        var area = node.GetRect();
        var list = new List<Node>();
        foreach (var n in Nodes)
        {
            if (n.Hidden || ReferenceEquals(n, node)) continue;
            if (NodeIntersects(n.GetRect(), area, partially)) list.Add(n);
        }
        return list;
    }

    /// <summary>True when the node overlaps (or is fully within) the given area.</summary>
    public bool IsNodeIntersecting(Node node, Rect area, bool partially = true)
        => NodeIntersects(node.GetRect(), area, partially);

    private static bool NodeIntersects(Rect node, Rect area, bool partially)
        => partially
            ? RectsIntersect(node, area)
            : node.Left >= area.Left && node.Right <= area.Right
              && node.Top >= area.Top && node.Bottom <= area.Bottom;

    /// <summary>Returns a serializable snapshot of the current nodes, edges and viewport.</summary>
    public FlowSnapshot ToObject() => new(Nodes.ToList(), Edges.ToList(), _viewport);

    public void Refresh(bool remeasure)
    {
        if (remeasure) _needsMeasure = true;
        StateHasChanged();
    }

    public void CapturePointer(ElementReference element, long pointerId)
        => _ = _interop.SetPointerCaptureAsync(element, pointerId);

    public void RegisterViewportPortal(object key, RenderFragment fragment)
    {
        // Only force a re-render when the portal is newly added; updating an existing
        // key's fragment on every cascade render would cause an infinite render loop.
        var isNew = !_viewportPortals.ContainsKey(key);
        _viewportPortals[key] = fragment;
        if (isNew) StateHasChanged();
    }

    public void UnregisterViewportPortal(object key)
    {
        if (_viewportPortals.Remove(key))
            StateHasChanged();
    }

    public void StartResize(string nodeId, ResizeDirection direction,
        double clientX, double clientY, double minWidth, double minHeight, bool keepAspectRatio)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        _resizing = true;
        _resizeNode = node;
        _resizeDir = direction;
        _resizeStartClientX = clientX;
        _resizeStartClientY = clientY;
        _resizeStartPos = node.Position;
        _resizeStartSize = node.EffectiveSize;
        _resizeMinW = minWidth;
        _resizeMinH = minHeight;
        _resizeKeepAspect = keepAspectRatio;
        StateHasChanged();
    }

    private void ApplyResize(PointerEventArgs e)
    {
        var node = _resizeNode!;
        var dx = (e.ClientX - _resizeStartClientX) / _viewport.Zoom;
        var dy = (e.ClientY - _resizeStartClientY) / _viewport.Zoom;

        double w = _resizeStartSize.Width;
        double h = _resizeStartSize.Height;
        double x = _resizeStartPos.X;
        double y = _resizeStartPos.Y;

        if (_resizeDir.HasFlag(ResizeDirection.Right))
            w = _resizeStartSize.Width + dx;
        if (_resizeDir.HasFlag(ResizeDirection.Bottom))
            h = _resizeStartSize.Height + dy;
        if (_resizeDir.HasFlag(ResizeDirection.Left))
        {
            w = _resizeStartSize.Width - dx;
            x = _resizeStartPos.X + dx;
        }
        if (_resizeDir.HasFlag(ResizeDirection.Top))
        {
            h = _resizeStartSize.Height - dy;
            y = _resizeStartPos.Y + dy;
        }

        // Clamp to minimums, keeping the anchored edge fixed.
        if (w < _resizeMinW)
        {
            if (_resizeDir.HasFlag(ResizeDirection.Left))
                x -= _resizeMinW - w;
            w = _resizeMinW;
        }
        if (h < _resizeMinH)
        {
            if (_resizeDir.HasFlag(ResizeDirection.Top))
                y -= _resizeMinH - h;
            h = _resizeMinH;
        }

        if (_resizeKeepAspect)
        {
            var aspect = _resizeStartSize.Width / Math.Max(1, _resizeStartSize.Height);
            h = w / aspect;
        }

        node.Width = w;
        node.Height = h;
        node.Position = new XYPosition(x, y);
    }

    public void FitView() => FitView(0.1);

    public void FitView(double padding)
    {
        var visible = Nodes.Where(n => !n.Hidden).ToList();
        if (visible.Count == 0 || _paneRect is null) return;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var n in visible)
        {
            var r = n.GetRect();
            minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y);
            maxX = Math.Max(maxX, r.Right); maxY = Math.Max(maxY, r.Bottom);
        }

        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxY - minY);
        var paneW = _paneRect.Width;
        var paneH = _paneRect.Height;

        var zoom = Math.Clamp(
            Math.Min(paneW / w, paneH / h) * (1 - padding),
            MinZoom, MaxZoom);

        var x = paneW / 2 - (minX + w / 2) * zoom;
        var y = paneH / 2 - (minY + h / 2) * zoom;
        _viewport = new Viewport(x, y, zoom);
        _ = OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    // ---- utils ----

    private XYPosition ScreenToFlow(PointerEventArgs e)
    {
        var px = e.ClientX - (_paneRect?.Left ?? 0);
        var py = e.ClientY - (_paneRect?.Top ?? 0);
        return _viewport.ScreenToFlow(px, py);
    }

    /// <summary>Snaps a flow-space point to the configured grid when snapping is enabled.</summary>
    private XYPosition Snap(XYPosition p)
    {
        if (!SnapToGrid) return p;
        var gx = SnapGridX <= 0 ? 1 : SnapGridX;
        var gy = SnapGridY <= 0 ? 1 : SnapGridY;
        return new XYPosition(Math.Round(p.X / gx) * gx, Math.Round(p.Y / gy) * gy);
    }

    /// <summary>Clamps a node's (relative) position against its parent or the configured node extent.</summary>
    private XYPosition ClampNodePosition(Node node, XYPosition pos)
    {
        var size = node.EffectiveSize;

        if (node.ExtentParent && node.ParentId is { } pid)
        {
            var parent = Nodes.FirstOrDefault(n => n.Id == pid);
            if (parent is not null)
            {
                var ps = parent.EffectiveSize;
                var maxX = Math.Max(0, ps.Width - size.Width);
                var maxY = Math.Max(0, ps.Height - size.Height);
                return new XYPosition(Math.Clamp(pos.X, 0, maxX), Math.Clamp(pos.Y, 0, maxY));
            }
        }

        if (NodeExtent is { } ext && node.ParentId is null)
        {
            var maxX = Math.Max(ext.Left, ext.Right - size.Width);
            var maxY = Math.Max(ext.Top, ext.Bottom - size.Height);
            return new XYPosition(Math.Clamp(pos.X, ext.Left, maxX), Math.Clamp(pos.Y, ext.Top, maxY));
        }

        return pos;
    }

    /// <summary>Clamps the viewport so the visible flow region stays within <see cref="TranslateExtent"/>.</summary>
    private Viewport ClampViewport(Viewport vp)
    {
        if (TranslateExtent is not { } ext || _paneRect is null) return vp;

        var paneW = _paneRect.Width;
        var paneH = _paneRect.Height;
        var minX = paneW - ext.Right * vp.Zoom;
        var maxX = -ext.Left * vp.Zoom;
        var minY = paneH - ext.Bottom * vp.Zoom;
        var maxY = -ext.Top * vp.Zoom;

        var x = minX <= maxX ? Math.Clamp(vp.X, minX, maxX) : (minX + maxX) / 2;
        var y = minY <= maxY ? Math.Clamp(vp.Y, minY, maxY) : (minY + maxY) / 2;
        return vp with { X = x, Y = y };
    }

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string ColorModeClass => ColorMode switch
    {
        ColorMode.Dark => "blazorflow--dark",
        ColorMode.System => "blazorflow--system",
        _ => string.Empty,
    };

    private static Models.Position ParsePosition(string? s) => s switch
    {
        "top" => Models.Position.Top,
        "right" => Models.Position.Right,
        "bottom" => Models.Position.Bottom,
        "left" => Models.Position.Left,
        _ => Models.Position.Bottom,
    };

    private static Models.Position OppositePosition(Models.Position p) => p switch
    {
        Models.Position.Top => Models.Position.Bottom,
        Models.Position.Bottom => Models.Position.Top,
        Models.Position.Left => Models.Position.Right,
        _ => Models.Position.Left,
    };

    public async ValueTask DisposeAsync()
    {
        if (_interop is not null)
            await _interop.DisposeAsync();
    }
}
