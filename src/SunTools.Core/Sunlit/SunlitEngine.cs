using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Clipper2Lib;
using SunTools.Core.Geometry;

namespace SunTools.Core.Sunlit;

/// <summary>Exact sunlit fraction of a window behind any triangulated shade.</summary>
public static class SunlitEngine
{
    /// <summary>At or below this value of s·n the sun is behind or parallel to the glass.</summary>
    public const double GrazingCosine = 1e-9;

    /// <summary>Computes one hour.</summary>
    /// <param name="sun">Direction towards the sun (any non-zero length).</param>
    /// <param name="dni">Direct normal irradiance in W/m² (≥ 0).</param>
    /// <param name="includePolygons">Also return the sunlit polygons (preview).</param>
    public static HourResult ComputeHour(Window window, ShadeMesh shade, Vec3 sun, double dni, bool includePolygons = false)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (shade == null) throw new ArgumentNullException(nameof(shade));
        var unitSun = ValidateSun(sun, 0);
        ValidateDni(dni, 0);
        return ComputeHour(PreparedScene.Build(window, shade), unitSun, dni, includePolygons);
    }

    /// <summary>Computes every hour; the scene is prepared once.</summary>
    /// <param name="suns">Directions towards the sun, one per hour.</param>
    /// <param name="dni">Direct normal irradiance per hour in W/m².</param>
    /// <param name="parallel">Compute hours in parallel (results are identical either way).</param>
    public static SunlitSeries Compute(Window window, ShadeMesh shade, IReadOnlyList<Vec3> suns,
        IReadOnlyList<double> dni, bool parallel = true)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (shade == null) throw new ArgumentNullException(nameof(shade));
        if (suns == null) throw new ArgumentNullException(nameof(suns));
        if (dni == null) throw new ArgumentNullException(nameof(dni));
        if (suns.Count != dni.Count)
            throw new ArgumentException($"Got {suns.Count} sun vectors but {dni.Count} DNI values.", nameof(dni));

        var unitSuns = new Vec3[suns.Count];
        for (int i = 0; i < suns.Count; i++)
        {
            unitSuns[i] = ValidateSun(suns[i], i);
            ValidateDni(dni[i], i);
        }

        var scene = PreparedScene.Build(window, shade);
        var fraction = new double[unitSuns.Length];
        var irradiance = new double[unitSuns.Length];
        void One(int i)
        {
            var r = ComputeHour(scene, unitSuns[i], dni[i], includePolygons: false);
            fraction[i] = r.SunlitFraction;
            irradiance[i] = r.DirectIrradiance;
        }

        if (parallel)
            Parallel.For(0, unitSuns.Length, One);
        else
            for (int i = 0; i < unitSuns.Length; i++) One(i);

        return new SunlitSeries(fraction, irradiance, scene.DegenerateTriangleCount);
    }

    internal static HourResult ComputeHour(PreparedScene scene, Vec3 unitSun, double dni, bool includePolygons)
    {
        var w = scene.Window;
        double cos = unitSun.Dot(w.Normal);
        if (cos <= GrazingCosine)
            return HourResult.NoSun;

        // Projection along the sun onto the glass plane: P' = P − (P_w / s_w)·s.
        double du = unitSun.Dot(w.U) / cos;
        double dv = unitSun.Dot(w.V) / cos;
        var clip = new Paths64(scene.Pieces.Count);
        var projected = new List<Point2>(4);
        foreach (var piece in scene.Pieces)
        {
            projected.Clear();
            foreach (var p in piece)
                projected.Add(new Point2(p.X - p.Z * du, p.Y - p.Z * dv));
            var inside = ConvexClip.ClipToRect(projected, w.MinX, w.MinY, w.MaxX, w.MaxY);
            if (inside.Count < 3)
                continue;
            // NonZero sums signed windings: a clockwise piece (−1) over a counter-clockwise one
            // (+1) would cancel. Orienting every piece positively makes NonZero a plain union.
            var path = ClipperConvert.ToPath64(inside);
            if (!Clipper.IsPositive(path))
                path.Reverse();
            clip.Add(path);
        }

        Paths64 sunlit;
        if (clip.Count == 0)
        {
            sunlit = new Paths64 { scene.WindowPath };
        }
        else
        {
            var clipper = new Clipper64();
            clipper.AddSubject(new Paths64 { scene.WindowPath });
            clipper.AddClip(clip);
            sunlit = new Paths64();
            clipper.Execute(ClipType.Difference, FillRule.NonZero, sunlit);
        }

        double f = Math.Min(1, Math.Abs(Clipper.Area(sunlit)) / scene.WindowPathArea);
        var polygons = includePolygons ? ToWorld(w, sunlit) : HourResult.NoPolygons;
        return new HourResult(f, f * dni * cos, polygons);
    }

    internal static Vec3 ValidateSun(Vec3 sun, int index)
    {
        if (!sun.IsFinite || !(sun.Length > 1e-12))
            throw new ArgumentException($"Sun vector at index {index} is zero or not finite.", nameof(sun));
        return sun.Normalized();
    }

    internal static void ValidateDni(double dni, int index)
    {
        if (!Vec3.IsFiniteNumber(dni) || dni < 0)
            throw new ArgumentException($"DNI at index {index} is negative or not finite.", nameof(dni));
    }

    private static IReadOnlyList<IReadOnlyList<Vec3>> ToWorld(Window w, Paths64 paths)
    {
        var result = new List<IReadOnlyList<Vec3>>(paths.Count);
        foreach (var path in paths)
        {
            var poly = new Vec3[path.Count];
            for (int i = 0; i < path.Count; i++)
                poly[i] = w.ToWorld(ClipperConvert.ToPoint2(path[i]));
            result.Add(poly);
        }
        return result;
    }
}
