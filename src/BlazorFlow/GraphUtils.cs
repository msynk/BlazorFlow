using BlazorFlow.Models;

namespace BlazorFlow;

/// <summary>
/// Pure helper functions for working with nodes and edges, mirroring React Flow's
/// exported utility functions (addEdge, getConnectedEdges, getIncomers, etc.).
/// </summary>
public static class GraphUtils
{
    /// <summary>
    /// Adds a connection as a new edge to <paramref name="edges"/> unless an equivalent
    /// edge already exists. Returns the same list for chaining. Mirrors <c>addEdge</c>.
    /// </summary>
    public static List<Edge> AddEdge(Connection connection, List<Edge> edges)
    {
        if (connection.Source is null || connection.Target is null) return edges;

        var exists = edges.Any(e =>
            e.Source == connection.Source &&
            e.Target == connection.Target &&
            e.SourceHandle == connection.SourceHandle &&
            e.TargetHandle == connection.TargetHandle);

        if (!exists)
        {
            edges.Add(new Edge
            {
                Id = $"e-{connection.Source}{connection.SourceHandle}-{connection.Target}{connection.TargetHandle}-{Guid.NewGuid():N}".Replace(" ", ""),
                Source = connection.Source,
                Target = connection.Target,
                SourceHandle = connection.SourceHandle,
                TargetHandle = connection.TargetHandle,
            });
        }
        return edges;
    }

    /// <summary>
    /// Updates an existing edge's endpoints to match <paramref name="newConnection"/>.
    /// Mirrors <c>reconnectEdge</c>. Returns the edges list for chaining.
    /// </summary>
    public static List<Edge> ReconnectEdge(Edge oldEdge, Connection newConnection, List<Edge> edges)
    {
        var target = edges.FirstOrDefault(e => e.Id == oldEdge.Id);
        if (target is not null && newConnection.Source is not null && newConnection.Target is not null)
        {
            target.Source = newConnection.Source;
            target.Target = newConnection.Target;
            target.SourceHandle = newConnection.SourceHandle;
            target.TargetHandle = newConnection.TargetHandle;
        }
        return edges;
    }

    /// <summary>Returns all edges connected to any of the given nodes. Mirrors <c>getConnectedEdges</c>.</summary>
    public static List<Edge> GetConnectedEdges(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var ids = nodes.Select(n => n.Id).ToHashSet();
        return edges.Where(e => ids.Contains(e.Source) || ids.Contains(e.Target)).ToList();
    }

    /// <summary>Returns the nodes that have an edge pointing to <paramref name="node"/>. Mirrors <c>getIncomers</c>.</summary>
    public static List<Node> GetIncomers(Node node, IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var sources = edges.Where(e => e.Target == node.Id).Select(e => e.Source).ToHashSet();
        return nodes.Where(n => sources.Contains(n.Id)).ToList();
    }

    /// <summary>Returns the nodes that <paramref name="node"/> has an edge pointing to. Mirrors <c>getOutgoers</c>.</summary>
    public static List<Node> GetOutgoers(Node node, IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var targets = edges.Where(e => e.Source == node.Id).Select(e => e.Target).ToHashSet();
        return nodes.Where(n => targets.Contains(n.Id)).ToList();
    }

    /// <summary>Returns the bounding rectangle (flow space) of the given nodes. Mirrors <c>getNodesBounds</c>.</summary>
    public static Rect GetNodesBounds(IEnumerable<Node> nodes)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        var any = false;
        foreach (var n in nodes)
        {
            any = true;
            var r = n.GetRect();
            minX = Math.Min(minX, r.X);
            minY = Math.Min(minY, r.Y);
            maxX = Math.Max(maxX, r.Right);
            maxY = Math.Max(maxY, r.Bottom);
        }
        return any ? new Rect(minX, minY, maxX - minX, maxY - minY) : new Rect(0, 0, 0, 0);
    }

    /// <summary>Computes the viewport that fits <paramref name="bounds"/> into a pane of the given size. Mirrors <c>getViewportForBounds</c>.</summary>
    public static Viewport GetViewportForBounds(
        Rect bounds, double paneWidth, double paneHeight,
        double minZoom, double maxZoom, double padding = 0.1)
    {
        var w = Math.Max(1, bounds.Width);
        var h = Math.Max(1, bounds.Height);
        var zoom = Math.Clamp(Math.Min(paneWidth / w, paneHeight / h) * (1 - padding), minZoom, maxZoom);
        var x = paneWidth / 2 - (bounds.X + w / 2) * zoom;
        var y = paneHeight / 2 - (bounds.Y + h / 2) * zoom;
        return new Viewport(x, y, zoom);
    }

    // ---- floating edges (port of React Flow's floating-edge utils) ----

    /// <summary>
    /// Returns the point on <paramref name="intersectionNode"/>'s border that lies on the line
    /// toward <paramref name="targetNode"/>'s center. Port of React Flow's <c>getNodeIntersection</c>.
    /// </summary>
    public static XYPosition GetNodeIntersection(Node intersectionNode, Node targetNode)
    {
        var iSize = intersectionNode.EffectiveSize;
        var iPos = intersectionNode.AbsolutePosition;
        var tSize = targetNode.EffectiveSize;
        var tPos = targetNode.AbsolutePosition;

        var w = iSize.Width / 2;
        var h = iSize.Height / 2;

        var x2 = iPos.X + w;
        var y2 = iPos.Y + h;
        var x1 = tPos.X + tSize.Width / 2;
        var y1 = tPos.Y + tSize.Height / 2;

        if (w == 0 || h == 0) return new XYPosition(x2, y2);

        var xx1 = (x1 - x2) / (2 * w) - (y1 - y2) / (2 * h);
        var yy1 = (x1 - x2) / (2 * w) + (y1 - y2) / (2 * h);
        var denom = Math.Abs(xx1) + Math.Abs(yy1);
        if (denom == 0) return new XYPosition(x2, y2);
        var a = 1 / denom;
        var xx3 = a * xx1;
        var yy3 = a * yy1;
        var x = w * (xx3 + yy3) + x2;
        var y = h * (-xx3 + yy3) + y2;
        return new XYPosition(x, y);
    }

    /// <summary>Returns which side of <paramref name="node"/> the given border point sits on. Port of <c>getEdgePosition</c>.</summary>
    public static Position GetEdgePosition(Node node, XYPosition point)
    {
        var size = node.EffectiveSize;
        var pos = node.AbsolutePosition;
        var nx = Math.Round(pos.X);
        var ny = Math.Round(pos.Y);
        var px = Math.Round(point.X);
        var py = Math.Round(point.Y);

        if (px <= nx + 1) return Position.Left;
        if (px >= nx + size.Width - 1) return Position.Right;
        if (py <= ny + 1) return Position.Top;
        if (py >= ny + size.Height - 1) return Position.Bottom;
        return Position.Top;
    }

    /// <summary>
    /// Computes floating edge endpoints (anchored to each node's border facing the other node).
    /// Returns the source/target points and the sides they attach to.
    /// </summary>
    public static (XYPosition Source, Position SourcePos, XYPosition Target, Position TargetPos)
        GetFloatingEdgeParams(Node source, Node target)
    {
        var sourcePoint = GetNodeIntersection(source, target);
        var targetPoint = GetNodeIntersection(target, source);
        return (
            sourcePoint, GetEdgePosition(source, sourcePoint),
            targetPoint, GetEdgePosition(target, targetPoint));
    }
}
