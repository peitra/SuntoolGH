using SunTools.Core.Geometry;
using SunTools.Core.Sunlit;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Sunlit;

public class PreparedSceneTests
{
    [Fact]
    public void Zero_area_triangles_are_counted_and_skipped()
    {
        var shade = new ShadeMesh(new[] { V(0, 0, 1), V(1, 0, 1), V(2, 0, 1) }, new[] { 0, 1, 2 });
        var scene = PreparedScene.Build(RectWindow(1, 1), shade);
        Assert.Equal(1, scene.DegenerateTriangleCount);
        Assert.Empty(scene.Pieces);
    }

    [Fact]
    public void Geometry_behind_the_glass_is_dropped()
    {
        var scene = PreparedScene.Build(RectWindow(1, 1), Plate(0, 0, 1, 1, -0.5));
        Assert.Empty(scene.Pieces);
        Assert.Equal(0, scene.DegenerateTriangleCount);
    }

    [Fact]
    public void Geometry_lying_on_the_glass_plane_is_dropped()
    {
        Assert.Empty(PreparedScene.Build(RectWindow(1, 1), Plate(0, 0, 1, 1, 0)).Pieces);
    }

    [Fact]
    public void Triangle_crossing_the_glass_keeps_only_its_front_part()
    {
        var shade = new ShadeMesh(new[] { V(0, 0, 1), V(1, 0, 1), V(0, 1, -1) }, new[] { 0, 1, 2 });
        var scene = PreparedScene.Build(RectWindow(1, 1), shade);
        var piece = Assert.Single(scene.Pieces);
        Assert.Equal(4, piece.Length);
        Assert.All(piece, p => Assert.True(p.Z >= 0));
    }

    [Fact]
    public void Window_path_area_matches_scaled_outline()
    {
        var scene = PreparedScene.Build(RectWindow(2, 1), ShadeMesh.Empty);
        // Normalised outline is 1 × 0.5; Clipper area is in (1e9)² units.
        Close(0.5, scene.WindowPathArea / (ClipperConvert.Scale * ClipperConvert.Scale), 1e-12);
    }
}
