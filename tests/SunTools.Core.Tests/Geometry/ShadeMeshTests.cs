using System;
using SunTools.Core.Geometry;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Geometry;

public class ShadeMeshTests
{
    [Fact]
    public void Counts_triangles()
    {
        Assert.Equal(2, Plate(0, 0, 1, 1, 1).TriangleCount);
        Assert.Equal(0, ShadeMesh.Empty.TriangleCount);
    }

    [Fact]
    public void Rejects_index_count_not_multiple_of_three()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ShadeMesh(new[] { V(0, 0, 0), V(1, 0, 0) }, new[] { 0, 1 }));
        Assert.Contains("multiple of 3", ex.Message);
    }

    [Fact]
    public void Rejects_out_of_range_index()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ShadeMesh(new[] { V(0, 0, 0), V(1, 0, 0), V(0, 1, 0) }, new[] { 0, 1, 3 }));
        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void Rejects_non_finite_vertex()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShadeMesh(new[] { V(0, 0, 0), V(1, double.NaN, 0), V(0, 1, 0) }, new[] { 0, 1, 2 }));
    }
}
