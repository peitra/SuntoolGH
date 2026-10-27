using System;
using SunTools.Core.Geometry;

namespace SunTools.Core.Solar;

/// <summary>Converts astronomical angles into world-frame sun vectors (Z up).</summary>
public static class SunVectors
{
    private const double HorizontalTolerance = 1e-9; // |north.Z| relative to |north|

    /// <summary>
    /// Unit vector pointing towards the sun. <paramref name="azimuth"/> from north, eastward, and
    /// <paramref name="zenith"/> in degrees. <paramref name="north"/> must be horizontal and non-zero;
    /// east is north turned 90° clockwise seen from above.
    /// </summary>
    public static Vec3 FromAngles(double azimuth, double zenith, Vec3 north)
    {
        var n = UnitNorth(north);
        var east = new Vec3(n.Y, -n.X, 0);
        double a = azimuth * Math.PI / 180, z = zenith * Math.PI / 180;
        return Math.Sin(z) * (Math.Sin(a) * east + Math.Cos(a) * n) + Math.Cos(z) * new Vec3(0, 0, 1);
    }

    internal static Vec3 UnitNorth(Vec3 north)
    {
        double length = north.Length;
        if (!north.IsFinite || length == 0)
            throw new ArgumentException($"north vector must be finite and non-zero, got {north}.", nameof(north));
        if (Math.Abs(north.Z) > HorizontalTolerance * length)
            throw new ArgumentException($"north vector must be horizontal (Z = 0), got {north}.", nameof(north));
        return new Vec3(north.X / length, north.Y / length, 0);
    }
}
