using System;
using SunTools.Core.Geometry;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Geometry;

public class WindowTests
{
    [Fact]
    public void Rectangle_has_expected_frame_area_and_scale()
    {
        var w = RectWindow(2, 1);
        Close(2, w.Area, 1e-12);
        Close(2, w.Scale, 1e-12);
        Close(V(0, 0, 1), w.Normal, 1e-12);
        Close(V(1, 0.5, 0), w.Origin, 1e-12);
        Close(V(1, 0, 0), w.U, 1e-12);
        Close(V(0, 1, 0), w.V, 1e-12);
    }

    [Fact]
    public void Local_and_world_coordinates_round_trip()
    {
        var w = RectWindow(2, 1);
        var local = w.ToLocal(V(2, 1, 0));
        Close(0.5, local.X, 1e-12);
        Close(0.25, local.Y, 1e-12);
        var back = w.ToWorld(new Point2(local.X, local.Y));
        Close(2, back.X, 1e-12);
        Close(1, back.Y, 1e-12);
    }

    [Fact]
    public void Closing_duplicate_vertex_is_removed()
    {
        var w = Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0), V(0, 0, 0) });
        Assert.Equal(4, w.Vertices.Count);
        Close(1, w.Area, 1e-12);
    }

    [Fact]
    public void Repeated_consecutive_vertex_is_removed()
    {
        var w = Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0) });
        Assert.Equal(4, w.Vertices.Count);
        Close(1, w.Area, 1e-12);
    }

    [Fact]
    public void Closing_vertex_within_modelling_tolerance_is_removed()
    {
        var w = Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0), V(1e-7, 0, 0) });
        Assert.Equal(4, w.Vertices.Count);
        Close(1, w.Area, 1e-6);
    }

    [Fact]
    public void Concave_window_is_accepted()
    {
        var w = Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 0.5, 0), V(0.5, 0.5, 0), V(0.5, 1, 0), V(0, 1, 0) });
        Close(0.75, w.Area, 1e-12);
    }

    [Fact]
    public void Explicit_opposite_normal_is_respected()
    {
        var w = Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0) }, V(0, 0, -5));
        Close(V(0, 0, -1), w.Normal, 1e-12);
        Close(1, w.U.Cross(w.V).Dot(w.Normal), 1e-12);
    }

    [Theory]
    [InlineData("too few")]
    [InlineData("collinear")]
    [InlineData("non-planar")]
    [InlineData("self-intersecting")]
    [InlineData("nan")]
    [InlineData("tilted normal")]
    [InlineData("zero normal")]
    public void Invalid_windows_are_rejected(string kind)
    {
        Action create = kind switch
        {
            "too few" => () => Window.Create(new[] { V(0, 0, 0), V(1, 0, 0) }),
            "collinear" => () => Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(2, 0, 0) }),
            "non-planar" => () => Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 1, 0.01), V(0, 1, 0) }),
            "self-intersecting" => () => Window.Create(new[] { V(0, 0, 0), V(3, 1, 0), V(3, 0, 0), V(0, 2, 0) }),
            "nan" => () => Window.Create(new[] { V(0, 0, 0), V(double.NaN, 0, 0), V(1, 1, 0) }),
            "tilted normal" => () => Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0) }, V(0, 1, 1)),
            _ => () => Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0) }, V(0, 0, 0)),
        };
        var ex = Assert.Throws<ArgumentException>(create);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Self_intersection_message_is_explicit()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Window.Create(new[] { V(0, 0, 0), V(3, 1, 0), V(3, 0, 0), V(0, 2, 0) }));
        Assert.Contains("intersects itself", ex.Message);
    }
}
