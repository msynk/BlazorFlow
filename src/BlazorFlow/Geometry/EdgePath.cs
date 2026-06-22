using System.Globalization;
using BlazorFlow.Models;

namespace BlazorFlow.Geometry;

/// <summary>
/// The result of computing an edge path: the SVG path data plus the
/// label anchor point (path midpoint).
/// </summary>
public readonly record struct PathResult(string Path, double LabelX, double LabelY);

/// <summary>
/// Pure functions that compute SVG path data for the built-in edge types.
/// These are C# ports of React Flow's edge path algorithms.
/// </summary>
public static class EdgePath
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static PathResult Compute(
        EdgeType type,
        double sx, double sy, Position sourcePos,
        double tx, double ty, Position targetPos) => type switch
    {
        EdgeType.Straight => Straight(sx, sy, tx, ty),
        EdgeType.Step => SmoothStep(sx, sy, sourcePos, tx, ty, targetPos, borderRadius: 0),
        EdgeType.SmoothStep => SmoothStep(sx, sy, sourcePos, tx, ty, targetPos, borderRadius: 5),
        _ => Bezier(sx, sy, sourcePos, tx, ty, targetPos),
    };

    public static PathResult Straight(double sx, double sy, double tx, double ty)
    {
        var labelX = (sx + tx) / 2;
        var labelY = (sy + ty) / 2;
        return new PathResult(F($"M {sx},{sy} L {tx},{ty}"), labelX, labelY);
    }

    // ---- Bezier ----

    public static PathResult Bezier(
        double sx, double sy, Position sourcePos,
        double tx, double ty, Position targetPos,
        double curvature = 0.25)
    {
        var (c1x, c1y) = ControlWithCurvature(sourcePos, sx, sy, tx, ty, curvature);
        var (c2x, c2y) = ControlWithCurvature(targetPos, tx, ty, sx, sy, curvature);

        // Cubic bezier midpoint (t = 0.5).
        var labelX = 0.125 * sx + 0.375 * c1x + 0.375 * c2x + 0.125 * tx;
        var labelY = 0.125 * sy + 0.375 * c1y + 0.375 * c2y + 0.125 * ty;

        var path = F($"M{sx},{sy} C{c1x},{c1y} {c2x},{c2y} {tx},{ty}");
        return new PathResult(path, labelX, labelY);
    }

    private static double ControlOffset(double distance, double curvature)
        => distance >= 0 ? 0.5 * distance : curvature * 25 * Math.Sqrt(-distance);

    private static (double x, double y) ControlWithCurvature(
        Position pos, double x1, double y1, double x2, double y2, double c) => pos switch
    {
        Position.Left => (x1 - ControlOffset(x1 - x2, c), y1),
        Position.Right => (x1 + ControlOffset(x2 - x1, c), y1),
        Position.Top => (x1, y1 - ControlOffset(y1 - y2, c)),
        Position.Bottom => (x1, y1 + ControlOffset(y2 - y1, c)),
        _ => (x1, y1),
    };

    // ---- SmoothStep / Step (orthogonal routing with rounded corners) ----

    public static PathResult SmoothStep(
        double sx, double sy, Position sourcePos,
        double tx, double ty, Position targetPos,
        double borderRadius = 5, double offset = 20)
    {
        var source = new XYPosition(sx, sy);
        var target = new XYPosition(tx, ty);

        var points = GetStepPoints(source, sourcePos, target, targetPos, offset,
            out var labelX, out var labelY);

        var path = BuildRoundedPath(points, borderRadius);
        return new PathResult(path, labelX, labelY);
    }

    private static (double x, double y) Dir(Position p) => p switch
    {
        Position.Left => (-1, 0),
        Position.Right => (1, 0),
        Position.Top => (0, -1),
        Position.Bottom => (0, 1),
        _ => (0, 0),
    };

    private static List<XYPosition> GetStepPoints(
        XYPosition source, Position sourcePos,
        XYPosition target, Position targetPos,
        double offset, out double labelX, out double labelY)
    {
        var (sdx, sdy) = Dir(sourcePos);
        var (tdx, tdy) = Dir(targetPos);

        var sourceGap = new XYPosition(source.X + sdx * offset, source.Y + sdy * offset);
        var targetGap = new XYPosition(target.X + tdx * offset, target.Y + tdy * offset);

        // Determine primary travel axis.
        bool horizontal = sourcePos is Position.Left or Position.Right;
        var dirAccessorX = (sourcePos is Position.Left or Position.Right);

        var centerX = (sourceGap.X + targetGap.X) / 2;
        var centerY = (sourceGap.Y + targetGap.Y) / 2;
        labelX = (source.X + target.X) / 2;
        labelY = (source.Y + target.Y) / 2;

        double sAcc = dirAccessorX ? sdx : sdy;
        double tAcc = dirAccessorX ? tdx : tdy;

        var inner = new List<XYPosition>();

        if (sAcc * tAcc == -1)
        {
            // Handles face opposite directions on the travel axis: split in the middle.
            var verticalSplit = new[]
            {
                new XYPosition(centerX, sourceGap.Y),
                new XYPosition(centerX, targetGap.Y),
            };
            var horizontalSplit = new[]
            {
                new XYPosition(sourceGap.X, centerY),
                new XYPosition(targetGap.X, centerY),
            };
            inner.AddRange(dirAccessorX ? verticalSplit : horizontalSplit);
        }
        else
        {
            // Same / perpendicular directions: single corner.
            var sourceTarget = new XYPosition(sourceGap.X, targetGap.Y);
            var targetSource = new XYPosition(targetGap.X, sourceGap.Y);
            inner.Add(dirAccessorX ? targetSource : sourceTarget);
        }

        var points = new List<XYPosition> { source, sourceGap };
        points.AddRange(inner);
        points.Add(targetGap);
        points.Add(target);
        return points;
    }

    private static string BuildRoundedPath(IReadOnlyList<XYPosition> points, double radius)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (i > 0 && i < points.Count - 1 && radius > 0)
            {
                sb.Append(Bend(points[i - 1], p, points[i + 1], radius));
            }
            else
            {
                sb.Append(i == 0 ? F($"M{p.X} {p.Y}") : F($"L{p.X} {p.Y}"));
            }
        }
        return sb.ToString();
    }

    private static string Bend(XYPosition a, XYPosition b, XYPosition c, double size)
    {
        var bendSize = Math.Min(Math.Min(Distance(a, b) / 2, Distance(b, c) / 2), size);
        double x = b.X, y = b.Y;

        // Collinear: no bend needed.
        if ((Eq(a.X, x) && Eq(x, c.X)) || (Eq(a.Y, y) && Eq(y, c.Y)))
            return F($"L{x} {y}");

        if (Eq(a.Y, y))
        {
            int xDir = a.X < c.X ? -1 : 1;
            int yDir = a.Y < c.Y ? 1 : -1;
            return F($"L {x + bendSize * xDir} {y}Q {x} {y} {x} {y + bendSize * yDir}");
        }

        int xDir2 = a.X < c.X ? 1 : -1;
        int yDir2 = a.Y < c.Y ? -1 : 1;
        return F($"L {x} {y + bendSize * yDir2}Q {x} {y} {x + bendSize * xDir2} {y}");
    }

    private static double Distance(XYPosition a, XYPosition b)
        => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private static bool Eq(double a, double b) => Math.Abs(a - b) < 0.0001;

    private static string F(FormattableString s) => s.ToString(Inv);
}
