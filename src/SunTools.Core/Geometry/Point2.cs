namespace SunTools.Core.Geometry;

/// <summary>Immutable 2D point in a window's normalised local frame.</summary>
public readonly struct Point2
{
    public Point2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }

    public override string ToString() => $"({X}, {Y})";
}
