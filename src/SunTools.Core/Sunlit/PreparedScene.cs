using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using SunTools.Core.Geometry;

namespace SunTools.Core.Sunlit;

/// <summary>Sun-independent preprocessing of one window and one shade state.</summary>
internal sealed class PreparedScene
{
    private const double DegenerateAreaTolerance = 1e-12; // × Scale²
    private const double CoplanarTolerance = 1e-9;        // normalised units

    private PreparedScene(Window window, List<Vec3[]> pieces, int degenerate, Path64 windowPath)
    {
        Window = window;
        Pieces = pieces;
        DegenerateTriangleCount = degenerate;
        WindowPath = windowPath;
        WindowPathArea = Math.Abs(Clipper.Area(windowPath));
    }

    public Window Window { get; }
    /// <summary>Front parts (Z ≥ 0) of shade triangles in normalised local coordinates.</summary>
    public IReadOnlyList<Vec3[]> Pieces { get; }
    public int DegenerateTriangleCount { get; }
    public Path64 WindowPath { get; }
    public double WindowPathArea { get; }

    public static PreparedScene Build(Window window, ShadeMesh shade)
    {
        var pieces = new List<Vec3[]>(shade.TriangleCount);
        int degenerate = 0;
        double minArea = DegenerateAreaTolerance * window.Scale * window.Scale;
        var vertices = shade.Vertices;
        var triangles = shade.Triangles;
        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vec3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            if ((b - a).Cross(c - a).Length / 2 <= minArea)
            {
                degenerate++;
                continue;
            }
            var front = ConvexClip.KeepFront(new[] { window.ToLocal(a), window.ToLocal(b), window.ToLocal(c) });
            // Geometry behind the glass, or lying on its plane, casts no shadow on it.
            if (front.Count < 3 || front.Max(p => p.Z) <= CoplanarTolerance)
                continue;
            pieces.Add(front.ToArray());
        }
        return new PreparedScene(window, pieces, degenerate, ClipperConvert.ToPath64(window.Outline));
    }
}
