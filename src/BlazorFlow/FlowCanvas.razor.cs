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

    /// <summary>Overlay content: place <c>Background</c>, <c>Controls</c>, <c>MiniMap</c>, <c>Panel</c> here.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public EventCallback<Connection> OnConnect { get; set; }

    /// <summary>
    /// Optional predicate to validate a prospective connection while the user drags.
    /// Return false to reject; rejected targets are highlighted differently.
    /// </summary>
    [Parameter] public Func<Connection, bool>? IsValidConnection { get; set; }
    [Parameter] public EventCallback<List<Node>> NodesChanged { get; set; }
    [Parameter] public EventCallback<List<Edge>> EdgesChanged { get; set; }
    [Parameter] public EventCallback<Node> OnNodeClick { get; set; }
    [Parameter] public EventCallback<Edge> OnEdgeClick { get; set; }
    [Parameter] public EventCallback<Viewport> OnViewportChanged { get; set; }

    [Parameter] public double MinZoom { get; set; } = 0.2;
    [Parameter] public double MaxZoom { get; set; } = 2.5;
    [Parameter] public EdgeType DefaultEdgeType { get; set; } = EdgeType.Bezier;
    [Parameter] public bool FitViewOnInit { get; set; }
    [Parameter] public bool PanOnDrag { get; set; } = true;
    [Parameter] public bool ZoomOnScroll { get; set; } = true;
    [Parameter] public bool NodesDraggable { get; set; } = true;
    [Parameter] public bool ElementsSelectable { get; set; } = true;
    [Parameter] public bool DeleteKeyEnabled { get; set; } = true;

    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }

    // ---- state ----

    private ElementReference _paneRef;
    private ElementReference _viewportRef;
    private readonly string _markerId = $"bf-arrow-{Guid.NewGuid():N}";

    private Viewport _viewport = Viewport.Identity;
    private DomRect? _paneRect;
    private FlowInterop _interop = default!;

    private bool _needsMeasure = true;
    private bool _firstRenderDone;

    // pan
    private bool _panning;
    private double _panStartX, _panStartY;
    private Viewport _panStartViewport;

    // node drag
    private bool _draggingNodes;
    private double _dragStartClientX, _dragStartClientY;
    private readonly List<(Node node, XYPosition start)> _dragSet = [];

    // connection
    private bool _connecting;
    private ConnectionSource _connSource;
    private XYPosition _connEnd;
    private (string nodeId, string? handleId, HandleType type)? _connHover;

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
            _firstRenderDone = true;
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
        // Render shallower nodes first so group containers sit behind their children.
        return Nodes.Where(n => !n.Hidden).OrderBy(Depth);
    }

    private IEnumerable<Edge> VisibleEdges()
    {
        ComputeLayout();
        return Edges.Where(e => !e.Hidden);
    }

    /// <summary>Resolves each node's absolute position by walking its parent chain.</summary>
    private void ComputeLayout()
    {
        var index = Nodes.ToDictionary(n => n.Id);
        foreach (var n in Nodes)
            n.AbsolutePosition = ResolveAbsolute(n, index, 0);
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

    private PathResult GetEdgeGeometry(Edge edge)
    {
        var source = Nodes.FirstOrDefault(n => n.Id == edge.Source);
        var target = Nodes.FirstOrDefault(n => n.Id == edge.Target);
        if (source is null || target is null)
            return new PathResult(string.Empty, 0, 0);

        var (sx, sy, sPos) = ResolveAnchor(source, HandleType.Source, edge.SourceHandle, Models.Position.Bottom);
        var (tx, ty, tPos) = ResolveAnchor(target, HandleType.Target, edge.TargetHandle, Models.Position.Top);

        return EdgePath.Compute(edge.Type, sx, sy, sPos, tx, ty, tPos);
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
        var result = EdgePath.Bezier(s.Anchor.X, s.Anchor.Y, s.Position, _connEnd.X, _connEnd.Y, endPos);
        return result.Path;
    }

    // ---- pane interactions ----

    private async Task OnPanePointerDown(PointerEventArgs e)
    {
        // Reaching here means the pointer hit the pane background (nodes/handles stop propagation).
        if (ElementsSelectable && !e.ShiftKey)
            ClearSelection();

        if (PanOnDrag && e.Button == 0)
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
            _viewport = _panStartViewport with
            {
                X = _panStartViewport.X + (e.ClientX - _panStartX),
                Y = _panStartViewport.Y + (e.ClientY - _panStartY),
            };
            StateHasChanged();
        }
        else if (_draggingNodes)
        {
            var dx = (e.ClientX - _dragStartClientX) / _viewport.Zoom;
            var dy = (e.ClientY - _dragStartClientY) / _viewport.Zoom;
            foreach (var (node, start) in _dragSet)
                node.Position = new XYPosition(start.X + dx, start.Y + dy);
            StateHasChanged();
        }
        else if (_connecting)
        {
            _connEnd = ScreenToFlow(e);
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
            _dragSet.Clear();
            _needsMeasure = true; // node moved: re-measure handle anchors
            await NodesChanged.InvokeAsync(Nodes);
        }

        if (_connecting)
        {
            // Released over empty space => cancel.
            CancelConnection();
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
        if (!ZoomOnScroll) return;
        if (_paneRect is null) await RefreshPaneRectAsync();

        var factor = e.DeltaY < 0 ? 1.15 : 1 / 1.15;
        var newZoom = Math.Clamp(_viewport.Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _viewport.Zoom) < 1e-6) return;

        var px = e.ClientX - (_paneRect?.Left ?? 0);
        var py = e.ClientY - (_paneRect?.Top ?? 0);

        // Keep the flow point under the cursor stationary.
        var flowX = (px - _viewport.X) / _viewport.Zoom;
        var flowY = (py - _viewport.Y) / _viewport.Zoom;
        _viewport = new Viewport(px - flowX * newZoom, py - flowY * newZoom, newZoom);

        await OnViewportChanged.InvokeAsync(_viewport);
        StateHasChanged();
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (DeleteKeyEnabled && e.Key is "Delete" or "Backspace")
            await DeleteSelectionAsync();
    }

    // ---- node drag / selection ----

    private void StartNodeDrag(Node node, PointerEventArgs e)
    {
        if (ElementsSelectable)
        {
            if (!node.Selected && !e.ShiftKey) ClearSelection();
            node.Selected = true;
        }
        _ = OnNodeClick.InvokeAsync(node);

        if (!NodesDraggable || !node.Draggable) return;

        _draggingNodes = true;
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
    }

    private void SelectEdge(Edge edge, PointerEventArgs e)
    {
        if (ElementsSelectable)
        {
            if (!e.ShiftKey) ClearSelection();
            edge.Selected = true;
        }
        _ = OnEdgeClick.InvokeAsync(edge);
        StateHasChanged();
    }

    private void ClearSelection()
    {
        foreach (var n in Nodes) n.Selected = false;
        foreach (var ed in Edges) ed.Selected = false;
    }

    private async Task DeleteSelectionAsync()
    {
        var removedNodes = Nodes.Where(n => n.Selected).Select(n => n.Id).ToHashSet();
        if (removedNodes.Count > 0)
        {
            Nodes.RemoveAll(n => removedNodes.Contains(n.Id));
            Edges.RemoveAll(ed => removedNodes.Contains(ed.Source) || removedNodes.Contains(ed.Target));
        }
        Edges.RemoveAll(ed => ed.Selected);

        await NodesChanged.InvokeAsync(Nodes);
        await EdgesChanged.InvokeAsync(Edges);
        StateHasChanged();
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
        _ = RefreshPaneRectAsync();
        StateHasChanged();
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
            Edges.Add(edge);
            _ = OnConnect.InvokeAsync(connection);
            _ = EdgesChanged.InvokeAsync(Edges);
        }

        CancelConnection();
    }

    private void CancelConnection()
    {
        _connecting = false;
        _connHover = null;
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
        if (handleType == _connSource.Type) return false;          // source->source / target->target invalid
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

    public void Refresh(bool remeasure)
    {
        if (remeasure) _needsMeasure = true;
        StateHasChanged();
    }

    public void CapturePointer(ElementReference element, long pointerId)
        => _ = _interop.SetPointerCaptureAsync(element, pointerId);

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
