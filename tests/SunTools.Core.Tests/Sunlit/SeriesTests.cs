using System;
using System.Linq;
using SunTools.Core.Geometry;
using SunTools.Core.Sunlit;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Sunlit;

public class SeriesTests
{
    private static readonly Vec3[] Suns =
    {
        Sun(0.1, 0.5, 1), Sun(0, 0, 1), Sun(0, 0, -1), Sun(0.6, 0.1, 1), Sun(-0.4, 0.9, 0.2),
    };
    private static readonly double[] Dni = { 800, 600, 500, 300, 100 };

    private static ShadeMesh Shade() =>
        Merge(Quad(V(-1000, 1, 0), V(1002, 1, 0), V(1002, 1, 0.4), V(-1000, 1, 0.4)),
              Quad(V(2, -1000, 0), V(2, 1001, 0), V(2, 1001, 0.5), V(2, -1000, 0.5)));

    [Fact]
    public void Series_matches_hour_by_hour_computation()
    {
        var window = RectWindow(2, 1);
        var series = SunlitEngine.Compute(window, Shade(), Suns, Dni);
        Assert.Equal(Suns.Length, series.Count);
        for (int i = 0; i < Suns.Length; i++)
        {
            var hour = SunlitEngine.ComputeHour(window, Shade(), Suns[i], Dni[i]);
            Assert.Equal(hour.SunlitFraction, series.SunlitFraction[i]);
            Assert.Equal(hour.DirectIrradiance, series.DirectIrradiance[i]);
        }
    }

    [Fact]
    public void Parallel_and_sequential_results_are_identical()
    {
        var window = RectWindow(2, 1);
        var a = SunlitEngine.Compute(window, Shade(), Suns, Dni, parallel: true);
        var b = SunlitEngine.Compute(window, Shade(), Suns, Dni, parallel: false);
        Assert.Equal(b.SunlitFraction.ToArray(), a.SunlitFraction.ToArray());
        Assert.Equal(b.DirectIrradiance.ToArray(), a.DirectIrradiance.ToArray());
    }

    [Fact]
    public void Degenerate_triangle_count_is_reported()
    {
        var shade = Merge(Shade(), new ShadeMesh(new[] { V(0, 0, 1), V(1, 0, 1), V(2, 0, 1) }, new[] { 0, 1, 2 }));
        var series = SunlitEngine.Compute(RectWindow(2, 1), shade, Suns, Dni);
        Assert.Equal(1, series.DegenerateTriangleCount);
    }

    [Fact]
    public void Invalid_sun_reports_its_index()
    {
        var suns = Suns.ToArray();
        suns[2] = V(0, 0, 0);
        var ex = Assert.Throws<ArgumentException>(() => SunlitEngine.Compute(RectWindow(2, 1), Shade(), suns, Dni));
        Assert.Contains("index 2", ex.Message);
    }

    [Fact]
    public void Invalid_dni_reports_its_index()
    {
        var dni = Dni.ToArray();
        dni[3] = -5;
        var ex = Assert.Throws<ArgumentException>(() => SunlitEngine.Compute(RectWindow(2, 1), Shade(), Suns, dni));
        Assert.Contains("index 3", ex.Message);
    }

    [Fact]
    public void Mismatched_lengths_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => SunlitEngine.Compute(RectWindow(2, 1), Shade(), Suns, Dni.Take(4).ToArray()));
    }
}
