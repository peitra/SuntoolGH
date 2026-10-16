using System;
using System.Collections.Generic;
using SunTools.Core.Sunlit;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Randomized;

public class OracleTests
{
    public static IEnumerable<object[]> Seeds => RandomScenes.Seeds(500);

    [Fact]
    public void Oracle_reproduces_the_analytic_overhang()
    {
        var overhang = Quad(V(-1000, 1, 0), V(1002, 1, 0), V(1002, 1, 0.4), V(-1000, 1, 0.4));
        Assert.InRange(RayOracle.SunlitFraction(RectWindow(2, 1), overhang, Sun(0.1, 0.5, 1)), 0.795, 0.805);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Engine_agrees_with_the_ray_casting_oracle(int seed)
    {
        var scene = RandomScenes.Create(seed);
        double engine = SunlitEngine.ComputeHour(scene.Window, scene.Shade, scene.Sun, 0).SunlitFraction;
        double oracle = RayOracle.SunlitFraction(scene.Window, scene.Shade, scene.Sun);
        Assert.True(Math.Abs(engine - oracle) <= 0.01, $"seed {seed}: engine {engine:F4}, oracle {oracle:F4}");
    }
}
