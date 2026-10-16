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
}
