namespace BlazorFlow.Models;

/// <summary>
/// A point in flow coordinate space (pre-viewport-transform).
/// </summary>
public readonly record struct XYPosition(double X, double Y)
{
    public static readonly XYPosition Zero = new(0, 0);

    public XYPosition Add(XYPosition other) => new(X + other.X, Y + other.Y);
    public XYPosition Subtract(XYPosition other) => new(X - other.X, Y - other.Y);

    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}

/// <summary>
/// A measured width/height in pixels (at zoom = 1).
/// </summary>
public readonly record struct Dimensions(double Width, double Height)
{
    public static readonly Dimensions Empty = new(0, 0);
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// An axis-aligned rectangle in flow coordinate space.
/// </summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
