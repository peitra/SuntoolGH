using System;
using System.Linq;
using System.Threading.Tasks;
using SunTools.Core.Geometry;

namespace SunTools.Core.Tests.Randomized;

/// <summary>Brute-force reference: an n × n grid of points on the window, one ray towards the sun
/// per point, tested against every triangle (Möller–Trumbore). Slow but obviously correct.
/// The default grid size is prime: with n = 400, k evenly spaced slats (k dividing 400) put every
/// slat edge at the same offset from the grid rows, and the discretisation errors add up.</summary>
internal static class RayOracle
{
    public static double SunlitFraction(Window window, ShadeMesh shade, Vec3 sun, int n = 401)
    {
        var s = sun.Normalized();
        if (s.Dot(window.Normal) <= 1e-9) return 0;

        var outline = window.Vertices
            .Select(p => { var d = p - window.Origin; return (X: d.Dot(window.U), Y: d.Dot(window.V)); })
            .ToArray();
        double minX = outline.Min(p => p.X), maxX = outline.Max(p => p.X);
        double minY = outline.Min(p => p.Y), maxY = outline.Max(p => p.Y);
        var triangles = Enumerable.Range(0, shade.TriangleCount)
            .Select(t => (A: shade.Vertices[shade.Triangles[3 * t]], B: shade.Vertices[shade.Triangles[3 * t + 1]], C: shade.Vertices[shade.Triangles[3 * t + 2]]))
            .ToArray();

        var inside = new int[n];
        var lit = new int[n];
        Parallel.For(0, n, j =>
        {
            double y = minY + (j + 0.5) * (maxY - minY) / n;
            for (int i = 0; i < n; i++)
            {
                double x = minX + (i + 0.5) * (maxX - minX) / n;
                if (!Contains(outline, x, y)) continue;
                inside[j]++;
                var origin = window.Origin + window.U * x + window.V * y;
                bool blocked = false;
                for (int k = 0; k < triangles.Length && !blocked; k++)
                    blocked = Hits(origin, s, triangles[k].A, triangles[k].B, triangles[k].C);
                if (!blocked) lit[j]++;
            }
        });
        return (double)lit.Sum() / inside.Sum();
    }

    private static bool Hits(Vec3 origin, Vec3 dir, Vec3 a, Vec3 b, Vec3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = dir.Cross(e2);
        double det = e1.Dot(p);
        if (Math.Abs(det) < 1e-12) return false;
        double inv = 1 / det;
        var tv = origin - a;
        double u = tv.Dot(p) * inv;
        if (u < 0 || u > 1) return false;
        var q = tv.Cross(e1);
        double v = dir.Dot(q) * inv;
        if (v < 0 || u + v > 1) return false;
        return e2.Dot(q) * inv > 1e-9;
    }

    private static bool Contains((double X, double Y)[] poly, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > y) != (poly[j].Y > y)
                && x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }
}
