using System;
using System.Collections.Generic;

namespace SunTools.Core.Geometry;

/// <summary>Sutherland–Hodgman clipping of convex polygons by half-spaces.</summary>
internal static class ConvexClip
{
    /// <summary>Keeps the part of a 3D convex polygon where <c>Z ≥ 0</c>.</summary>
    public static List<Vec3> KeepFront(IReadOnlyList<Vec3> poly) =>
        ClipHalfSpace(poly, p => p.Z, (a, b, t) => a + (b - a) * t);

    /// <summary>Keeps the part of a 2D convex polygon inside an axis-aligned rectangle.</summary>
    public static List<Point2> ClipToRect(IReadOnlyList<Point2> poly, double minX, double minY, double maxX, double maxY)
    {
        Func<Point2, Point2, double, Point2> lerp =
            (a, b, t) => new Point2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        var result = ClipHalfSpace(poly, p => p.X - minX, lerp);
        result = ClipHalfSpace(result, p => maxX - p.X, lerp);
        result = ClipHalfSpace(result, p => p.Y - minY, lerp);
        return ClipHalfSpace(result, p => maxY - p.Y, lerp);
    }

    /// <summary>Keeps the part of a convex polygon where <paramref name="dist"/> ≥ 0.</summary>
    private static List<T> ClipHalfSpace<T>(IReadOnlyList<T> poly, Func<T, double> dist, Func<T, T, double, T> lerp)
    {
        int n = poly.Count;
        var result = new List<T>(n + 1);
        for (int i = 0; i < n; i++)
        {
            T cur = poly[i];
            T next = poly[(i + 1) % n];
            double dc = dist(cur);
            double dn = dist(next);
            if (dc >= 0)
                result.Add(cur);
            // Interpolate from the kept endpoint: far projected points (grazing sun) would
            // otherwise lose precision through cancellation.
            if (dc > 0 && dn < 0)
                result.Add(lerp(cur, next, dc / (dc - dn)));
            else if (dc < 0 && dn > 0)
                result.Add(lerp(next, cur, dn / (dn - dc)));
        }
        return result;
    }
}
