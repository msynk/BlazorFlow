using BlazorFlow.Models;

namespace BlazorFlow.Demo.Examples;

/// <summary>
/// A lightweight layered (Sugiyama-style) graph layout used by the Layouting example,
/// standing in for dagre/elk in the React Flow original. Assigns each node a rank by its
/// longest path from a root, then spreads nodes evenly within each rank.
/// </summary>
public static class LayoutHelper
{
    public static void Layout(
        IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
        string direction = "TB", double nodeWidth = 172, double nodeHeight = 50,
        double rankGap = 120, double siblingGap = 40)
    {
        if (nodes.Count == 0) return;

        var byId = nodes.ToDictionary(n => n.Id);
        var incoming = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        var outgoing = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var e in edges)
        {
            if (!byId.ContainsKey(e.Source) || !byId.ContainsKey(e.Target)) continue;
            outgoing[e.Source].Add(e.Target);
            incoming[e.Target].Add(e.Source);
        }

        // Longest-path ranking (handles DAGs; cycles fall back to a guarded BFS depth).
        var rank = nodes.ToDictionary(n => n.Id, _ => 0);
        var roots = nodes.Where(n => incoming[n.Id].Count == 0).Select(n => n.Id).ToList();
        if (roots.Count == 0) roots = [nodes[0].Id];

        var queue = new Queue<string>(roots);
        var guard = 0;
        var maxIterations = nodes.Count * nodes.Count + nodes.Count + 1;
        while (queue.Count > 0 && guard++ < maxIterations)
        {
            var id = queue.Dequeue();
            foreach (var next in outgoing[id])
            {
                if (rank[next] < rank[id] + 1)
                {
                    rank[next] = rank[id] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        var levels = nodes
            .GroupBy(n => rank[n.Id])
            .OrderBy(g => g.Key)
            .ToList();

        var horizontal = direction is "LR" or "RL";

        foreach (var level in levels)
        {
            var ordered = level.ToList();
            var count = ordered.Count;
            for (var i = 0; i < count; i++)
            {
                var node = ordered[i];
                var along = i * ((horizontal ? nodeHeight : nodeWidth) + siblingGap)
                    - (count - 1) * ((horizontal ? nodeHeight : nodeWidth) + siblingGap) / 2;
                var across = level.Key * ((horizontal ? nodeWidth : nodeHeight) + rankGap);

                node.Position = horizontal
                    ? new XYPosition(across, along)
                    : new XYPosition(along, across);
            }
        }
    }
}
