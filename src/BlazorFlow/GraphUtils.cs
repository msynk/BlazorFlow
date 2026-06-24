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
}
