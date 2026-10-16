using System;
using SunTools.Core.Geometry;
using Xunit;

namespace SunTools.Core.Tests.Geometry;

public class Vec3Tests
{
    [Fact]
    public void Dot_and_cross_follow_the_right_hand_rule()
    {
        var x = new Vec3(1, 0, 0);
        var y = new Vec3(0, 1, 0);
        Assert.Equal(0, x.Dot(y));
        var z = x.Cross(y);
        Assert.Equal(0, z.X);
        Assert.Equal(0, z.Y);
        Assert.Equal(1, z.Z);
    }

    [Fact]
    public void Normalized_has_unit_length()
    {
        var v = new Vec3(3, 4, 12).Normalized();
        Assert.Equal(1, v.Length, 12);
        Assert.Equal(3.0 / 13, v.X, 12);
    }

    [Fact]
    public void Normalized_rejects_the_zero_vector()
    {
        Assert.Throws<InvalidOperationException>(() => new Vec3(0, 0, 0).Normalized());
    }

    [Fact]
    public void IsFinite_detects_nan_and_infinity()
    {
        Assert.True(new Vec3(1, 2, 3).IsFinite);
        Assert.False(new Vec3(double.NaN, 0, 0).IsFinite);
        Assert.False(new Vec3(0, double.PositiveInfinity, 0).IsFinite);
    }
}
