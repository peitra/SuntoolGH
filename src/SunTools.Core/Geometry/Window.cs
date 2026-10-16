using System;
using System.Collections.Generic;
using System.Linq;

namespace SunTools.Core.Geometry;

/// <summary>
/// A planar, simple window polygon with its outward normal and a local frame
/// (origin at the vertex average, U and V in-plane, lengths divided by <see cref="Scale"/>).
/// </summary>
public sealed class Window
{
    internal const double PlanarityTolerance = 1e-6;  // × Scale
    private const double DuplicateTolerance = 1e-6;   // × bounding diagonal
    private const double NormalTolerance = 1e-6;      // on |cos| between given and polygon normal
    private const double IntersectionEpsilon = 1e-12; // normalised units

    private Window(IReadOnlyList<Vec3> vertices, Vec3 normal, Vec3 origin, Vec3 u, Vec3 v,
        double scale, double area, Point2[] outline, double planeOffset)
    {
        _planeOffset = planeOffset;
        Vertices = vertices;
        Normal = normal;
        Origin = origin;
        U = u;
        V = v;
        Scale = scale;
        Area = area;
        Outline = outline;
        MinX = outline.Min(p => p.X);
        MaxX = outline.Max(p => p.X);
        MinY = outline.Min(p => p.Y);
        MaxY = outline.Max(p => p.Y);
    }

    // Distance from Origin to the glass plane along Normal. Origin is rounded in georeferenced
    // models, so it is generally not exactly on the plane.
    private readonly double _planeOffset;

    public IReadOnlyList<Vec3> Vertices { get; }
    /// <summary>Unit outward normal (the side the sun must be on).</summary>
    public Vec3 Normal { get; }
    public Vec3 Origin { get; }
    public Vec3 U { get; }
    public Vec3 V { get; }
    /// <summary>Largest extent of the window along U or V, in metres.</summary>
    public double Scale { get; }
    /// <summary>Window area in m².</summary>
    public double Area { get; }

    internal IReadOnlyList<Point2> Outline { get; }
    internal double MinX { get; }
    internal double MaxX { get; }
    internal double MinY { get; }
    internal double MaxY { get; }

    /// <summary>Validates the polygon and builds its frame.</summary>
    /// <param name="vertices">Polygon vertices in metres; repeated consecutive vertices (including a
    /// closing vertex equal to the first) are ignored.</param>
    /// <param name="normal">Outward normal; defaults to the right-hand rule on the vertex order.</param>
    /// <exception cref="ArgumentException">Fewer than 3 vertices, non-finite, zero area, non-planar,
    /// self-intersecting, or a normal not perpendicular to the plane.</exception>
    public static Window Create(IReadOnlyList<Vec3> vertices, Vec3? normal = null)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));
        var pts = new List<Vec3>(vertices);
        for (int i = 0; i < pts.Count; i++)
            if (!pts[i].IsFinite)
                throw new ArgumentException($"Window vertex {i} is not finite.", nameof(vertices));

        double diag = BoundingDiagonal(pts);
        pts = RemoveRepeatedVertices(pts, DuplicateTolerance * diag);
        if (pts.Count < 3)
            throw new ArgumentException("A window needs at least 3 distinct vertices.", nameof(vertices));

        // Newell normal relative to the first vertex: stable for georeferenced coordinates.
        var newell = new Vec3(0, 0, 0);
        for (int i = 1; i + 1 < pts.Count; i++)
            newell = newell + (pts[i] - pts[0]).Cross(pts[i + 1] - pts[0]);
        double area = newell.Length / 2;
        if (!(area > 1e-12 * diag * diag))
            throw new ArgumentException("Window has zero area.", nameof(vertices));
        var polygonNormal = newell / newell.Length;

        var w = polygonNormal;
        if (normal.HasValue)
        {
            var given = normal.Value;
            if (!given.IsFinite || !(given.Length > 0))
                throw new ArgumentException("Window normal is zero or not finite.", nameof(normal));
            double cos = given.Normalized().Dot(polygonNormal);
            if (Math.Abs(cos) < 1 - NormalTolerance)
                throw new ArgumentException("Window normal is not perpendicular to the window plane.", nameof(normal));
            w = cos > 0 ? polygonNormal : -polygonNormal;
        }

        var origin = pts.Aggregate(new Vec3(0, 0, 0), (acc, p) => acc + p) / pts.Count;
        Vec3 u = default;
        for (int i = 0; i < pts.Count; i++)
        {
            var edge = pts[(i + 1) % pts.Count] - pts[i];
            edge = edge - w * edge.Dot(w);
            if (edge.Length > 1e-9 * diag)
            {
                u = edge.Normalized();
                break;
            }
        }
        var v = w.Cross(u);

        var raw = pts.Select(p => { var d = p - origin; return new Vec3(d.Dot(u), d.Dot(v), d.Dot(w)); }).ToArray();
        double scale = Math.Max(raw.Max(p => p.X) - raw.Min(p => p.X), raw.Max(p => p.Y) - raw.Min(p => p.Y));
        double planeOffset = raw.Average(p => p.Z);
        if (raw.Any(p => Math.Abs(p.Z - planeOffset) > PlanarityTolerance * scale))
            throw new ArgumentException("Window is not planar.", nameof(vertices));

        var outline = raw.Select(p => new Point2(p.X / scale, p.Y / scale)).ToArray();
        if (SelfIntersects(outline))
            throw new ArgumentException("Window outline intersects itself.", nameof(vertices));

        return new Window(pts.ToArray(), w, origin, u, v, scale, area, outline, planeOffset);
    }

    /// <summary>World point → normalised local coordinates (Z along the normal).</summary>
    internal Vec3 ToLocal(Vec3 world)
    {
        var d = world - Origin;
        return new Vec3(d.Dot(U) / Scale, d.Dot(V) / Scale, (d.Dot(Normal) - _planeOffset) / Scale);
    }

    /// <summary>Normalised in-plane point → world point on the glass.</summary>
    internal Vec3 ToWorld(Point2 local) =>
        Origin + U * (local.X * Scale) + V * (local.Y * Scale) + Normal * _planeOffset;

    /// <summary>Drops each vertex closer than <paramref name="tolerance"/> to the previous kept one,
    /// and the closing vertex if it is that close to the first.</summary>
    private static List<Vec3> RemoveRepeatedVertices(List<Vec3> pts, double tolerance)
    {
        var kept = new List<Vec3>(pts.Count);
        foreach (var p in pts)
            if (kept.Count == 0 || (p - kept[kept.Count - 1]).Length > tolerance)
                kept.Add(p);
        while (kept.Count > 1 && (kept[kept.Count - 1] - kept[0]).Length <= tolerance)
            kept.RemoveAt(kept.Count - 1);
        return kept;
    }

    private static double BoundingDiagonal(IReadOnlyList<Vec3> pts)
    {
        if (pts.Count == 0) return 0;
        var min = new Vec3(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z));
        var max = new Vec3(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z));
        return (max - min).Length;
    }

    private static bool SelfIntersects(IReadOnlyList<Point2> poly)
    {
        int n = poly.Count;
        for (int i = 0; i < n; i++)
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // adjacent through the closing edge
                if (SegmentsTouch(poly[i], poly[(i + 1) % n], poly[j], poly[(j + 1) % n]))
                    return true;
            }
        return false;
    }

    private static bool SegmentsTouch(Point2 a, Point2 b, Point2 c, Point2 d)
    {
        double d1 = Orient(c, d, a), d2 = Orient(c, d, b), d3 = Orient(a, b, c), d4 = Orient(a, b, d);
        const double e = IntersectionEpsilon;
        if (((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e)))
            return true;
        return (Math.Abs(d1) <= e && OnSegment(c, d, a)) || (Math.Abs(d2) <= e && OnSegment(c, d, b))
            || (Math.Abs(d3) <= e && OnSegment(a, b, c)) || (Math.Abs(d4) <= e && OnSegment(a, b, d));
    }

    private static double Orient(Point2 o, Point2 a, Point2 b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    private static bool OnSegment(Point2 a, Point2 b, Point2 p) =>
        p.X >= Math.Min(a.X, b.X) - IntersectionEpsilon && p.X <= Math.Max(a.X, b.X) + IntersectionEpsilon
        && p.Y >= Math.Min(a.Y, b.Y) - IntersectionEpsilon && p.Y <= Math.Max(a.Y, b.Y) + IntersectionEpsilon;
}
