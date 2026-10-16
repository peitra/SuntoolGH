using System;
using System.Collections.Generic;
using System.Linq;

namespace SunTools.Core.Geometry;

/// <summary>Shade geometry as a triangle soup in world coordinates (metres).</summary>
public sealed class ShadeMesh
{
    /// <param name="vertices">Vertex positions.</param>
    /// <param name="triangles">Flat vertex indices, three per triangle; winding is irrelevant.</param>
    /// <exception cref="ArgumentException">Non-finite vertex, index count not a multiple of 3,
    /// or index out of range.</exception>
    public ShadeMesh(IReadOnlyList<Vec3> vertices, IReadOnlyList<int> triangles)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));
        if (triangles == null) throw new ArgumentNullException(nameof(triangles));
        if (triangles.Count % 3 != 0)
            throw new ArgumentException("Triangle index count must be a multiple of 3.", nameof(triangles));
        for (int i = 0; i < vertices.Count; i++)
            if (!vertices[i].IsFinite)
                throw new ArgumentException($"Shade vertex {i} is not finite.", nameof(vertices));
        for (int i = 0; i < triangles.Count; i++)
            if (triangles[i] < 0 || triangles[i] >= vertices.Count)
                throw new ArgumentException($"Triangle index {triangles[i]} at position {i} is out of range.", nameof(triangles));
        Vertices = vertices.ToArray();
        Triangles = triangles.ToArray();
    }

    public static ShadeMesh Empty { get; } = new ShadeMesh(Array.Empty<Vec3>(), Array.Empty<int>());

    public IReadOnlyList<Vec3> Vertices { get; }
    public IReadOnlyList<int> Triangles { get; }
    public int TriangleCount => Triangles.Count / 3;
}
