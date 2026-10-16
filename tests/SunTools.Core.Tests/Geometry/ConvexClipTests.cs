using System.Linq;
using SunTools.Core.Geometry;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Geometry;

public class ConvexClipTests
{
    [Fact]
    public void Triangle_fully_in_front_is_unchanged()
    {
        var tri = new[] { V(0, 0, 1), V(1, 0, 1), V(0, 1, 2) };
        Assert.Equal(tri, ConvexClip.KeepFront(tri));
    }

    [Fact]
    public void Triangle_fully_behind_is_removed()
    {
        Assert.Empty(ConvexClip.KeepFront(new[] { V(0, 0, -1), V(1, 0, -1), V(0, 1, -2) }));
    }

    [Fact]
    public void Triangle_with_one_vertex_behind_becomes_a_quadrilateral()
    {
        var result = ConvexClip.KeepFront(new[] { V(0, 0, 1), V(1, 0, 1), V(0, 1, -1) });
        Assert.Equal(4, result.Count);
        Assert.All(result, p => Assert.True(p.Z >= 0));
        Close(0.5, result[2].X, 1e-12);
        Close(0.5, result[2].Y, 1e-12);
        Close(0.5, result[3].Y, 1e-12);
    }

    [Fact]
    public void Vertex_on_the_plane_is_not_duplicated()
    {
        var result = ConvexClip.KeepFront(new[] { V(0, 0, 0), V(1, 0, 1), V(0, 1, -1) });
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Square_is_clipped_to_the_rectangle()
    {
        var square = new[] { new Point2(-1, -1), new Point2(2, -1), new Point2(2, 2), new Point2(-1, 2) };
        var result = ConvexClip.ClipToRect(square, 0, 0, 1, 1);
        Close(1, Area(result), 1e-12);
        Assert.All(result, p => Assert.InRange(p.X, 0, 1));
    }

    [Fact]
    public void Clipping_far_projections_keeps_full_precision()
    {
        var band = new[] { new Point2(0.5, 0.25), new Point2(1e9, 0.25), new Point2(1e9, 0.75), new Point2(0.5, 0.75) };
        var result = ConvexClip.ClipToRect(band, 0, 0, 1, 1);
        Close(0.25, Area(result), 1e-12);
        Close(1, result.Max(p => p.X), 1e-12);
    }
}
