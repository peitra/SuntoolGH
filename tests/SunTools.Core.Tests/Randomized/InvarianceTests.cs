using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using SunTools.Core.Geometry;
using SunTools.Core.Sunlit;
using Xunit;

namespace SunTools.Core.Tests.Randomized;

public class InvarianceTests
{
    public static IEnumerable<object[]> Seeds => RandomScenes.Seeds(100);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Rigid_motion_of_scene_and_sun_leaves_the_fraction_unchanged(int seed)
    {
        var scene = RandomScenes.Create(seed);
        var moved = scene.Transformed(RigidTransform.Random(new System.Random(10_000 + seed), 10_000));
        double f0 = SunlitEngine.ComputeHour(scene.Window, scene.Shade, scene.Sun, 0).SunlitFraction;
        double f1 = SunlitEngine.ComputeHour(moved.Window, moved.Shade, moved.Sun, 0).SunlitFraction;
        Assert.True(Math.Abs(f0 - f1) < 1e-7, $"seed {seed}: {f0:R} vs {f1:R}");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Sunlit_area_plus_independently_computed_shaded_area_equals_window_area(int seed)
    {
        var scene = RandomScenes.Create(seed, frontOnly: true);
        var window = scene.Window;
        var sun = scene.Sun.Normalized();
        double f = SunlitEngine.ComputeHour(window, scene.Shade, sun, 0).SunlitFraction;

        // Independent path: Clipper2 double API, Intersection instead of Difference, metres.
        double cos = sun.Dot(window.Normal);
        PointD Project(Vec3 p)
        {
            var d = p - window.Origin;
            var q = d - sun * (d.Dot(window.Normal) / cos);
            return new PointD(q.Dot(window.U), q.Dot(window.V));
        }
        var subject = new PathsD { new PathD(window.Vertices.Select(Project)) };
        var clip = new PathsD();
        var v = scene.Shade.Vertices;
        var t = scene.Shade.Triangles;
        for (int i = 0; i < t.Count; i += 3)
        {
            // Union semantics need every triangle positively oriented (NonZero sums windings).
            var tri = new PathD { Project(v[t[i]]), Project(v[t[i + 1]]), Project(v[t[i + 2]]) };
            if (!Clipper.IsPositive(tri)) tri.Reverse();
            clip.Add(tri);
        }
        double shaded = Math.Abs(Clipper.Area(Clipper.Intersect(subject, clip, FillRule.NonZero, 8)));

        double sunlit = f * window.Area;
        Assert.True(Math.Abs(sunlit + shaded - window.Area) < 1e-6 * window.Area,
            $"seed {seed}: sunlit {sunlit:R} + shaded {shaded:R} != window {window.Area:R}");
    }
}
