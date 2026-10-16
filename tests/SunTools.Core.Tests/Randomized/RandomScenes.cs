using System;
using System.Collections.Generic;
using System.Linq;
using SunTools.Core.Geometry;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Randomized;

internal sealed class Scene
{
    public Scene(Window window, ShadeMesh shade, Vec3 sun)
    {
        Window = window;
        Shade = shade;
        Sun = sun;
    }

    public Window Window { get; }
    public ShadeMesh Shade { get; }
    public Vec3 Sun { get; }

    public Scene Transformed(RigidTransform t) => new Scene(
        Window.Create(Window.Vertices.Select(t.Apply).ToArray(), t.Rotate(Window.Normal)),
        new ShadeMesh(Shade.Vertices.Select(t.Apply).ToArray(), Shade.Triangles),
        t.Rotate(Sun));
}

/// <summary>Deterministic random scenes: rectangular window, one of three shade kinds, random sun
/// in front of the glass, then a random rigid motion.</summary>
internal static class RandomScenes
{
    public static IEnumerable<object[]> Seeds(int count) => Enumerable.Range(0, count).Select(i => new object[] { i });

    /// <param name="frontOnly">Keep all shade geometry strictly in front of the glass.</param>
    public static Scene Create(int seed, bool frontOnly = false)
    {
        var rng = new System.Random(seed);
        double w = 1 + 2 * rng.NextDouble();
        double h = 1 + 2 * rng.NextDouble();
        var window = Window.Create(new[] { V(0, 0, 0), V(w, 0, 0), V(w, h, 0), V(0, h, 0) });
        var shade = (seed % 3) switch
        {
            0 => RandomTriangles(rng, w, h, frontOnly ? 0.05 : -0.5),
            1 => Slats(rng, w, h),
            _ => LPlate(rng, w, h),
        };
        double sz = 0.15 + 0.85 * rng.NextDouble();
        double phi = 2 * Math.PI * rng.NextDouble();
        double r = Math.Sqrt(1 - sz * sz);
        var local = new Scene(window, shade, V(r * Math.Cos(phi), r * Math.Sin(phi), sz));
        return local.Transformed(RigidTransform.Random(rng, 100));
    }

    private static ShadeMesh RandomTriangles(System.Random rng, double w, double h, double zMin)
    {
        int n = 1 + rng.Next(20);
        var vertices = new List<Vec3>();
        var triangles = new List<int>();
        for (int i = 0; i < 3 * n; i++)
        {
            vertices.Add(V(-1 + (w + 2) * rng.NextDouble(), -1 + (h + 2) * rng.NextDouble(), zMin + (1.5 - zMin) * rng.NextDouble()));
            triangles.Add(i);
        }
        return new ShadeMesh(vertices, triangles);
    }

    private static ShadeMesh Slats(System.Random rng, double w, double h)
    {
        int k = 2 + rng.Next(7);
        double depth = 0.1 + 0.2 * rng.NextDouble();
        double tilt = (rng.NextDouble() * 120 - 60) * Math.PI / 180;
        double dy = depth * Math.Sin(tilt), dz = depth * Math.Cos(tilt), z0 = 0.05;
        var slats = new List<ShadeMesh>();
        for (int i = 0; i < k; i++)
        {
            double y = (i + 0.5) * h / k;
            slats.Add(Quad(V(-0.5, y, z0), V(w + 0.5, y, z0), V(w + 0.5, y + dy, z0 + dz), V(-0.5, y + dy, z0 + dz)));
        }
        return Merge(slats.ToArray());
    }

    private static ShadeMesh LPlate(System.Random rng, double w, double h)
    {
        double z = 0.1 + 0.9 * rng.NextDouble();
        double x0 = -0.5 + rng.NextDouble(), y0 = -0.5 + rng.NextDouble();
        double a = 0.5 + w * rng.NextDouble(), b = 0.5 + h * rng.NextDouble();
        double cut = 0.3 + 0.4 * rng.NextDouble();
        var pts = new[]
        {
            V(x0, y0, z), V(x0 + a, y0, z), V(x0 + a, y0 + cut * b, z),
            V(x0 + cut * a, y0 + cut * b, z), V(x0 + cut * a, y0 + b, z), V(x0, y0 + b, z),
        };
        // Fan from vertex 0, which sees the whole L (the reflex vertex is 3).
        return new ShadeMesh(pts, new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 });
    }
}
