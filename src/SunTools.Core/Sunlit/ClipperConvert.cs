using System;
using System.Collections.Generic;
using Clipper2Lib;
using SunTools.Core.Geometry;

namespace SunTools.Core.Sunlit;

/// <summary>Conversion between normalised window coordinates and Clipper2 integers.</summary>
internal static class ClipperConvert
{
    /// <summary>Integer units per normalised unit (window ≈ 1 × 1 → 1e-9 resolution).</summary>
    public const double Scale = 1e9;

    public static Path64 ToPath64(IReadOnlyList<Point2> poly)
    {
        var path = new Path64(poly.Count);
        foreach (var p in poly)
            path.Add(new Point64((long)Math.Round(p.X * Scale), (long)Math.Round(p.Y * Scale)));
        return path;
    }

    public static Point2 ToPoint2(Point64 p) => new Point2(p.X / Scale, p.Y / Scale);
}
