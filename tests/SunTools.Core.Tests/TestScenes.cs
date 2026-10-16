using System;
using System.Collections.Generic;
using SunTools.Core.Geometry;
using Xunit;

namespace SunTools.Core.Tests;

internal static class TestScenes
{
    public static Vec3 V(double x, double y, double z) => new Vec3(x, y, z);

    /// <summary>Unsigned shoelace area of a 2D polygon.</summary>
    public static double Area(IReadOnlyList<Point2> poly)
    {
        double twice = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            twice += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(twice) / 2;
    }

    public static void Close(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");

    /// <summary>Component-wise comparison (avoids bitwise struct equality and −0.0 vs 0.0).</summary>
    public static void Close(Vec3 expected, Vec3 actual, double tolerance)
    {
        Close(expected.X, actual.X, tolerance);
        Close(expected.Y, actual.Y, tolerance);
        Close(expected.Z, actual.Z, tolerance);
    }

    /// <summary>Rectangle [0,w]×[0,h] in plane z = 0, outward normal +Z.</summary>
    public static Window RectWindow(double w, double h) =>
        Window.Create(new[] { V(0, 0, 0), V(w, 0, 0), V(w, h, 0), V(0, h, 0) });

    public static ShadeMesh Quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d) =>
        new ShadeMesh(new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 });

    /// <summary>Axis-aligned plate [x0,x1]×[y0,y1] parallel to the z = 0 window, at height z.</summary>
    public static ShadeMesh Plate(double x0, double y0, double x1, double y1, double z) =>
        Quad(V(x0, y0, z), V(x1, y0, z), V(x1, y1, z), V(x0, y1, z));

    public static ShadeMesh Merge(params ShadeMesh[] parts)
    {
        var vertices = new List<Vec3>();
        var triangles = new List<int>();
        foreach (var part in parts)
        {
            int offset = vertices.Count;
            vertices.AddRange(part.Vertices);
            foreach (int t in part.Triangles) triangles.Add(t + offset);
        }
        return new ShadeMesh(vertices, triangles);
    }

    public static Vec3 Sun(double x, double y, double z) => V(x, y, z).Normalized();

    /// <summary>Area of a planar 3D polygon, signed along <paramref name="normal"/>.</summary>
    public static double SignedArea(IReadOnlyList<Vec3> poly, Vec3 normal)
    {
        var acc = new Vec3(0, 0, 0);
        for (int i = 1; i + 1 < poly.Count; i++)
            acc = acc + (poly[i] - poly[0]).Cross(poly[i + 1] - poly[0]);
        return acc.Dot(normal) / 2;
    }
}
