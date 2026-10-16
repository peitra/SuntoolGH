# SunTools Core Engine Implementation Plan (Plan 1 of 4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 2017 code with a Rhino-free .NET library that computes the exact hourly sunlit fraction of a window and the direct irradiance on the glass for any triangulated shade, proven by analytic, invariant and ray-casting tests under CI on Windows and macOS.

**Architecture:** `SunTools.Core` (netstandard2.0) holds the geometry: a `Window` with its normalised local frame, a `ShadeMesh` triangle soup, and `SunlitEngine`. Each triangle is clipped to the outside of the glass plane once per scene. For each hour it is projected along the sun onto the glass plane and clipped to the window bounding box. Then one Clipper2 `Difference(window, projections)` with the NonZero fill rule gives the sunlit area. `SunTools.Core.Tests` (net8.0, xUnit) pins it with closed-form cases, rigid-motion invariance, an independent intersection-area check and a brute-force ray-casting oracle.

**Tech Stack:** .NET 8 SDK, C# (LangVersion latest), Clipper2 1.5.4 (NuGet, namespace `Clipper2Lib`), xUnit 2.9.2, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-15-suntools-v1-design.md` (sections 2, 3, 6, 7.1, 7.4).

**Follow-up plans (not in this plan):** Plan 2 — Solar (EPW reader, NREL SPA, spec §7.1 solar tests). Plan 3 — Grasshopper plugin (spec §4, sun-vector sign rule, meshing, units, Export Table). Plan 4 — EnergyPlus validation (spec §5 scene JSON, §7.2, §7.4 performance on the venetian case).

## Global Constraints

- `SunTools.Core` targets `netstandard2.0`; its only package dependency is `Clipper2` version `1.5.4`; it never references Rhino or Grasshopper.
- Tests target `net8.0` with xUnit; no Rhino dependency.
- Units: metres. Sun vectors point **towards** the sun. DNI in W/m².
- Sun behind or grazing the glass: `s·n ≤ 1e-9` → `f = 0`, `E_dir = 0`.
- Window planarity tolerance: `1e-6 × Scale`, where Scale is the largest extent of the window along U or V.
- Clipper2 integer scale: `1e9` on normalised window coordinates.
- Analytic tolerance `1e-6`; oracle tolerance `0.01` with a `400 × 400` grid.
- Invalid input → `ArgumentException` with an explicit message.
- Before any `dotnet` command: `export PATH="$HOME/.dotnet:$PATH"`.
- Every commit message ends with the line `Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK`, after a blank line.
- Public repository: commit only code, tests and documentation about the tool itself.

## Review Focus

1. The user passes the glass surface itself, or a wall coplanar with it, as shade geometry → it must not shade the window (`f = 1`, not `0`). Pinned in Task 4 and Task 5.
2. A closed Rhino polyline repeats its first vertex at the end → the window must be accepted as the same polygon. Pinned in Task 3.
3. A nearly grazing sun (`s·n` just above `1e-9`) projects geometry billions of units away → no integer overflow, `f` stays in [0, 1]. Pinned in Task 5.
4. Duplicated, overlapping or reversed-winding shade triangles (common in Rhino meshes) → shadow counted once. Pinned in Task 5.
5. Georeferenced models (coordinates ≈ 4,000,000 m) and very small windows (1 cm) → same exact fractions. Pinned in Task 5.

---

## File Structure

```
.gitignore                         rewritten: build outputs ignored
README.md                          rewritten: v1 status, legacy tag, build command
global.json                        pins .NET SDK 8.0 (latestFeature)
Directory.Build.props              LangVersion latest, Nullable enable
SunTools.sln                       new solution (Core + Core.Tests)
.github/workflows/ci.yml           build + test on windows-latest, macos-latest
src/SunTools.Core/
  SunTools.Core.csproj
  Geometry/Vec3.cs                 3D vector value type
  Geometry/Point2.cs               2D point value type
  Geometry/ConvexClip.cs           Sutherland–Hodgman half-space / rectangle clipping (internal)
  Geometry/Window.cs               validated window polygon + normalised local frame
  Geometry/ShadeMesh.cs            validated triangle soup
  Sunlit/ClipperConvert.cs         Point2 <-> Clipper2 Point64 at scale 1e9 (internal)
  Sunlit/PreparedScene.cs          per-scene preprocessing: degenerate count, front clipping (internal)
  Sunlit/HourResult.cs             one-hour result
  Sunlit/SunlitSeries.cs           multi-hour result
  Sunlit/SunlitEngine.cs           public entry points
tests/SunTools.Core.Tests/
  SunTools.Core.Tests.csproj
  TestScenes.cs                    scene builders and numeric helpers
  Geometry/Vec3Tests.cs
  Geometry/ConvexClipTests.cs
  Geometry/WindowTests.cs
  Geometry/ShadeMeshTests.cs
  Sunlit/PreparedSceneTests.cs
  Sunlit/AnalyticCaseTests.cs
  Sunlit/SeriesTests.cs
  Randomized/RigidTransform.cs
  Randomized/RandomScenes.cs
  Randomized/InvarianceTests.cs
  Randomized/RayOracle.cs
  Randomized/OracleTests.cs
```

Removed from the tree (kept under tag `v0-2017`): `SuntoolsGH/`, `packages/`, `.vs/`, `dev/`, the old `SunTools.sln`.

---

### Task 1: Toolchain, legacy freeze and solution skeleton with `Vec3`

**Files:**
- Delete: `SuntoolsGH/`, `packages/`, `.vs/`, `dev/`, `SunTools.sln`
- Create: `.gitignore`, `README.md` (overwrite), `global.json`, `Directory.Build.props`, `SunTools.sln`
- Create: `src/SunTools.Core/SunTools.Core.csproj`, `src/SunTools.Core/Geometry/Vec3.cs`, `src/SunTools.Core/Geometry/Point2.cs`
- Create: `tests/SunTools.Core.Tests/SunTools.Core.Tests.csproj`, `tests/SunTools.Core.Tests/Geometry/Vec3Tests.cs`

**Interfaces:**
- Produces: `SunTools.Core.Geometry.Vec3` (readonly struct: `X`, `Y`, `Z`, operators `+ - * /` and unary `-`, `Dot`, `Cross`, `Length`, `IsFinite`, `Normalized()` throwing `InvalidOperationException` on zero or non-finite). `SunTools.Core.Geometry.Point2` (readonly struct: `X`, `Y`).

- [ ] **Step 1: Install the .NET 8 SDK locally**

```bash
SCRATCH="${TMPDIR:-/tmp}"
curl -sSL https://dot.net/v1/dotnet-install.sh -o "$SCRATCH/dotnet-install.sh"
bash "$SCRATCH/dotnet-install.sh" --channel 8.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
dotnet --version
```
Expected: prints `8.0.x`.

- [ ] **Step 2: Freeze the 2017 code under a tag and remove it from the tree**

```bash
git tag -a v0-2017 1590d5a -m "2017 PhD-era SuntoolGH (Rhino 5, .NET 4.0), frozen before the v1 rewrite"
git rm -r -q SuntoolsGH packages .vs dev SunTools.sln
git status --short | head
```
Expected: the tag exists (`git tag` lists `v0-2017`), and the listed files show as deleted (`D`).

- [ ] **Step 3: Write repository files**

`.gitignore`:
```gitignore
bin/
obj/
.vs/
.idea/
*.user
*.suo
TestResults/
.DS_Store
```

`README.md`:
```markdown
# SuntoolGH

A Grasshopper plugin (Rhino 8, macOS and Windows) that evaluates the direct-sun
performance of actuated shading geometries: the hourly sunlit fraction of a window and
the direct irradiance on the glass, for every state of the shade.

**Status:** v1 rewrite in progress. The 2017 code is preserved at tag `v0-2017`.

- Design: `docs/superpowers/specs/2026-10-15-suntools-v1-design.md`
- Build and test: `dotnet test SunTools.sln` (.NET 8 SDK)

License: GPL-3.0 (see `LICENSE.md`). Contact: vc6@alumni.princeton.edu
```

`global.json`:
```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

`src/SunTools.Core/SunTools.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <RootNamespace>SunTools.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Clipper2" Version="1.5.4" />
    <InternalsVisibleTo Include="SunTools.Core.Tests" />
  </ItemGroup>
</Project>
```

`tests/SunTools.Core.Tests/SunTools.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\SunTools.Core\SunTools.Core.csproj" />
  </ItemGroup>
</Project>
```

Create the solution:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet new sln -n SunTools
dotnet sln SunTools.sln add src/SunTools.Core/SunTools.Core.csproj tests/SunTools.Core.Tests/SunTools.Core.Tests.csproj
```
Expected: two "Project ... added to the solution" lines.

- [ ] **Step 4: Write the failing test**

`tests/SunTools.Core.Tests/Geometry/Vec3Tests.cs`:
```csharp
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
```

- [ ] **Step 5: Run the test to verify it fails**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~Vec3Tests"`
Expected: build FAILS with `error CS0246: The type or namespace name 'Vec3' could not be found`.

- [ ] **Step 6: Implement `Vec3` and `Point2`**

`src/SunTools.Core/Geometry/Vec3.cs`:
```csharp
using System;

namespace SunTools.Core.Geometry;

/// <summary>Immutable 3D vector: metres in world space, normalised units in a window frame.</summary>
public readonly struct Vec3
{
    public Vec3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double k) => new Vec3(a.X * k, a.Y * k, a.Z * k);
    public static Vec3 operator *(double k, Vec3 a) => a * k;
    public static Vec3 operator /(Vec3 a, double k) => new Vec3(a.X / k, a.Y / k, a.Z / k);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;

    public Vec3 Cross(Vec3 b) => new Vec3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

    public double Length => Math.Sqrt(Dot(this));

    public bool IsFinite => IsFiniteNumber(X) && IsFiniteNumber(Y) && IsFiniteNumber(Z);

    /// <summary>Unit vector with the same direction.</summary>
    /// <exception cref="InvalidOperationException">The vector is zero or not finite.</exception>
    public Vec3 Normalized()
    {
        double length = Length;
        if (!(length > 0) || double.IsInfinity(length))
            throw new InvalidOperationException("Cannot normalise a zero or non-finite vector.");
        return this / length;
    }

    public override string ToString() => $"({X}, {Y}, {Z})";

    internal static bool IsFiniteNumber(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
}
```

`src/SunTools.Core/Geometry/Point2.cs`:
```csharp
namespace SunTools.Core.Geometry;

/// <summary>Immutable 2D point in a window's normalised local frame.</summary>
public readonly struct Point2
{
    public Point2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }

    public override string ToString() => $"({X}, {Y})";
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~Vec3Tests"`
Expected: `Passed!  - Failed: 0, Passed: 4`.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Replace 2017 code with v1 solution skeleton and Vec3

Legacy code is frozen at tag v0-2017.

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 2: Convex polygon clipping

**Files:**
- Create: `src/SunTools.Core/Geometry/ConvexClip.cs`
- Create: `tests/SunTools.Core.Tests/TestScenes.cs`
- Test: `tests/SunTools.Core.Tests/Geometry/ConvexClipTests.cs`

**Interfaces:**
- Consumes: `Vec3`, `Point2` (Task 1).
- Produces (internal): `ConvexClip.KeepFront(IReadOnlyList<Vec3> poly) : List<Vec3>` keeps the part with `Z ≥ 0`. `ConvexClip.ClipToRect(IReadOnlyList<Point2> poly, double minX, double minY, double maxX, double maxY) : List<Point2>`. Test helpers `TestScenes.V(x,y,z)`, `TestScenes.Area(IReadOnlyList<Point2>)`.

- [ ] **Step 1: Write the test helpers and the failing tests**

`tests/SunTools.Core.Tests/TestScenes.cs`:
```csharp
using System;
using System.Collections.Generic;
using SunTools.Core.Geometry;
using Xunit;

namespace SunTools.Core.Tests;

internal static class TestScenes
{
    public static Vec3 V(double x, double y, double z) => new Vec3(x, y, z);

    /// <summary>Unsigned shoelace area of a 2D polygon.</summary>
    public static double Area(IReadOnlyList<Point2> poly)
    {
        double twice = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            twice += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(twice) / 2;
    }

    public static void Close(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");

    /// <summary>Component-wise comparison (avoids bitwise struct equality and −0.0 vs 0.0).</summary>
    public static void Close(Vec3 expected, Vec3 actual, double tolerance)
    {
        Close(expected.X, actual.X, tolerance);
        Close(expected.Y, actual.Y, tolerance);
        Close(expected.Z, actual.Z, tolerance);
    }
}
```

`tests/SunTools.Core.Tests/Geometry/ConvexClipTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~ConvexClipTests"`
Expected: build FAILS with `The name 'ConvexClip' does not exist in the current context`.

- [ ] **Step 3: Implement `ConvexClip`**

`src/SunTools.Core/Geometry/ConvexClip.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace SunTools.Core.Geometry;

/// <summary>Sutherland–Hodgman clipping of convex polygons by half-spaces.</summary>
internal static class ConvexClip
{
    /// <summary>Keeps the part of a 3D convex polygon where <c>Z ≥ 0</c>.</summary>
    public static List<Vec3> KeepFront(IReadOnlyList<Vec3> poly) =>
        ClipHalfSpace(poly, p => p.Z, (a, b, t) => a + (b - a) * t);

    /// <summary>Keeps the part of a 2D convex polygon inside an axis-aligned rectangle.</summary>
    public static List<Point2> ClipToRect(IReadOnlyList<Point2> poly, double minX, double minY, double maxX, double maxY)
    {
        Func<Point2, Point2, double, Point2> lerp =
            (a, b, t) => new Point2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        var result = ClipHalfSpace(poly, p => p.X - minX, lerp);
        result = ClipHalfSpace(result, p => maxX - p.X, lerp);
        result = ClipHalfSpace(result, p => p.Y - minY, lerp);
        return ClipHalfSpace(result, p => maxY - p.Y, lerp);
    }

    /// <summary>Keeps the part of a convex polygon where <paramref name="dist"/> ≥ 0.</summary>
    private static List<T> ClipHalfSpace<T>(IReadOnlyList<T> poly, Func<T, double> dist, Func<T, T, double, T> lerp)
    {
        int n = poly.Count;
        var result = new List<T>(n + 1);
        for (int i = 0; i < n; i++)
        {
            T cur = poly[i];
            T next = poly[(i + 1) % n];
            double dc = dist(cur);
            double dn = dist(next);
            if (dc >= 0)
                result.Add(cur);
            // Interpolate from the kept endpoint: far projected points (grazing sun) would
            // otherwise lose precision through cancellation.
            if (dc > 0 && dn < 0)
                result.Add(lerp(cur, next, dc / (dc - dn)));
            else if (dc < 0 && dn > 0)
                result.Add(lerp(next, cur, dn / (dn - dc)));
        }
        return result;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~ConvexClipTests"`
Expected: `Passed!  - Failed: 0, Passed: 6`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add convex half-space and rectangle clipping

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 3: `Window` — validated polygon and normalised local frame

**Files:**
- Create: `src/SunTools.Core/Geometry/Window.cs`
- Modify: `tests/SunTools.Core.Tests/TestScenes.cs` (add `RectWindow`)
- Test: `tests/SunTools.Core.Tests/Geometry/WindowTests.cs`

**Interfaces:**
- Consumes: `Vec3`, `Point2`.
- Produces: `Window.Create(IReadOnlyList<Vec3> vertices, Vec3? normal = null) : Window`. Public properties: `IReadOnlyList<Vec3> Vertices` (closing duplicate removed), `Vec3 Normal` (unit, outward), `Vec3 Origin` (vertex average), `Vec3 U`, `Vec3 V` (`U × V = Normal`), `double Scale` (metres), `double Area` (m²). Internal: `IReadOnlyList<Point2> Outline` (normalised), `double MinX, MinY, MaxX, MaxY` (normalised bounding box), `Vec3 ToLocal(Vec3 world)`, `Vec3 ToWorld(Point2 local)`. Test helper `TestScenes.RectWindow(double w, double h)`: rectangle `[0,w]×[0,h]` in plane `z = 0`, normal `+Z`.

- [ ] **Step 1: Add the helper and write the failing tests**

Add to `TestScenes` (inside the class):
```csharp
    /// <summary>Rectangle [0,w]×[0,h] in plane z = 0, outward normal +Z.</summary>
    public static Window RectWindow(double w, double h) =>
        Window.Create(new[] { V(0, 0, 0), V(w, 0, 0), V(w, h, 0), V(0, h, 0) });
```

`tests/SunTools.Core.Tests/Geometry/WindowTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~WindowTests"`
Expected: build FAILS with `The type or namespace name 'Window' could not be found`.

- [ ] **Step 3: Implement `Window`**

`src/SunTools.Core/Geometry/Window.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace SunTools.Core.Geometry;

/// <summary>
/// A planar, simple window polygon with its outward normal and a local frame
/// (origin at the vertex average, U and V in-plane, lengths divided by <see cref="Scale"/>).
/// </summary>
public sealed class Window
{
    private const double PlanarityTolerance = 1e-6;   // × Scale
    private const double NormalTolerance = 1e-6;      // on |cos| between given and polygon normal
    private const double IntersectionEpsilon = 1e-12; // normalised units

    private Window(IReadOnlyList<Vec3> vertices, Vec3 normal, Vec3 origin, Vec3 u, Vec3 v,
        double scale, double area, Point2[] outline)
    {
        Vertices = vertices;
        Normal = normal;
        Origin = origin;
        U = u;
        V = v;
        Scale = scale;
        Area = area;
        Outline = outline;
        MinX = outline.Min(p => p.X);
        MaxX = outline.Max(p => p.X);
        MinY = outline.Min(p => p.Y);
        MaxY = outline.Max(p => p.Y);
    }

    public IReadOnlyList<Vec3> Vertices { get; }
    /// <summary>Unit outward normal (the side the sun must be on).</summary>
    public Vec3 Normal { get; }
    public Vec3 Origin { get; }
    public Vec3 U { get; }
    public Vec3 V { get; }
    /// <summary>Largest extent of the window along U or V, in metres.</summary>
    public double Scale { get; }
    /// <summary>Window area in m².</summary>
    public double Area { get; }

    internal IReadOnlyList<Point2> Outline { get; }
    internal double MinX { get; }
    internal double MaxX { get; }
    internal double MinY { get; }
    internal double MaxY { get; }

    /// <summary>Validates the polygon and builds its frame.</summary>
    /// <param name="vertices">Polygon vertices in metres; a repeated closing vertex is ignored.</param>
    /// <param name="normal">Outward normal; defaults to the right-hand rule on the vertex order.</param>
    /// <exception cref="ArgumentException">Fewer than 3 vertices, non-finite, zero area, non-planar,
    /// self-intersecting, or a normal not perpendicular to the plane.</exception>
    public static Window Create(IReadOnlyList<Vec3> vertices, Vec3? normal = null)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));
        var pts = new List<Vec3>(vertices);
        for (int i = 0; i < pts.Count; i++)
            if (!pts[i].IsFinite)
                throw new ArgumentException($"Window vertex {i} is not finite.", nameof(vertices));

        double diag = BoundingDiagonal(pts);
        if (pts.Count > 1 && (pts[pts.Count - 1] - pts[0]).Length <= 1e-9 * Math.Max(1.0, diag))
            pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3)
            throw new ArgumentException("A window needs at least 3 distinct vertices.", nameof(vertices));

        // Newell normal relative to the first vertex: stable for georeferenced coordinates.
        var newell = new Vec3(0, 0, 0);
        for (int i = 1; i + 1 < pts.Count; i++)
            newell = newell + (pts[i] - pts[0]).Cross(pts[i + 1] - pts[0]);
        double area = newell.Length / 2;
        if (!(area > 1e-12 * diag * diag))
            throw new ArgumentException("Window has zero area.", nameof(vertices));
        var polygonNormal = newell / newell.Length;

        var w = polygonNormal;
        if (normal.HasValue)
        {
            var given = normal.Value;
            if (!given.IsFinite || !(given.Length > 0))
                throw new ArgumentException("Window normal is zero or not finite.", nameof(normal));
            double cos = given.Normalized().Dot(polygonNormal);
            if (Math.Abs(cos) < 1 - NormalTolerance)
                throw new ArgumentException("Window normal is not perpendicular to the window plane.", nameof(normal));
            w = cos > 0 ? polygonNormal : -polygonNormal;
        }

        var origin = pts.Aggregate(new Vec3(0, 0, 0), (acc, p) => acc + p) / pts.Count;
        Vec3 u = default;
        for (int i = 0; i < pts.Count; i++)
        {
            var edge = pts[(i + 1) % pts.Count] - pts[i];
            edge = edge - w * edge.Dot(w);
            if (edge.Length > 1e-9 * diag)
            {
                u = edge.Normalized();
                break;
            }
        }
        var v = w.Cross(u);

        var raw = pts.Select(p => { var d = p - origin; return new Vec3(d.Dot(u), d.Dot(v), d.Dot(w)); }).ToArray();
        double scale = Math.Max(raw.Max(p => p.X) - raw.Min(p => p.X), raw.Max(p => p.Y) - raw.Min(p => p.Y));
        if (raw.Any(p => Math.Abs(p.Z) > PlanarityTolerance * scale))
            throw new ArgumentException("Window is not planar.", nameof(vertices));

        var outline = raw.Select(p => new Point2(p.X / scale, p.Y / scale)).ToArray();
        if (SelfIntersects(outline))
            throw new ArgumentException("Window outline intersects itself.", nameof(vertices));

        return new Window(pts.ToArray(), w, origin, u, v, scale, area, outline);
    }

    /// <summary>World point → normalised local coordinates (Z along the normal).</summary>
    internal Vec3 ToLocal(Vec3 world)
    {
        var d = world - Origin;
        return new Vec3(d.Dot(U) / Scale, d.Dot(V) / Scale, d.Dot(Normal) / Scale);
    }

    /// <summary>Normalised in-plane point → world point on the glass.</summary>
    internal Vec3 ToWorld(Point2 local) => Origin + U * (local.X * Scale) + V * (local.Y * Scale);

    private static double BoundingDiagonal(IReadOnlyList<Vec3> pts)
    {
        if (pts.Count == 0) return 0;
        var min = new Vec3(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z));
        var max = new Vec3(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z));
        return (max - min).Length;
    }

    private static bool SelfIntersects(IReadOnlyList<Point2> poly)
    {
        int n = poly.Count;
        for (int i = 0; i < n; i++)
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // adjacent through the closing edge
                if (SegmentsTouch(poly[i], poly[(i + 1) % n], poly[j], poly[(j + 1) % n]))
                    return true;
            }
        return false;
    }

    private static bool SegmentsTouch(Point2 a, Point2 b, Point2 c, Point2 d)
    {
        double d1 = Orient(c, d, a), d2 = Orient(c, d, b), d3 = Orient(a, b, c), d4 = Orient(a, b, d);
        const double e = IntersectionEpsilon;
        if (((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e)))
            return true;
        return (Math.Abs(d1) <= e && OnSegment(c, d, a)) || (Math.Abs(d2) <= e && OnSegment(c, d, b))
            || (Math.Abs(d3) <= e && OnSegment(a, b, c)) || (Math.Abs(d4) <= e && OnSegment(a, b, d));
    }

    private static double Orient(Point2 o, Point2 a, Point2 b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    private static bool OnSegment(Point2 a, Point2 b, Point2 p) =>
        p.X >= Math.Min(a.X, b.X) - IntersectionEpsilon && p.X <= Math.Max(a.X, b.X) + IntersectionEpsilon
        && p.Y >= Math.Min(a.Y, b.Y) - IntersectionEpsilon && p.Y <= Math.Max(a.Y, b.Y) + IntersectionEpsilon;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~WindowTests"`
Expected: `Passed!  - Failed: 0, Passed: 13`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add validated Window with normalised local frame

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 4: `ShadeMesh` and per-scene preparation

**Files:**
- Create: `src/SunTools.Core/Geometry/ShadeMesh.cs`, `src/SunTools.Core/Sunlit/ClipperConvert.cs`, `src/SunTools.Core/Sunlit/PreparedScene.cs`
- Modify: `tests/SunTools.Core.Tests/TestScenes.cs` (add `Quad`, `Plate`, `Merge`, `Sun`)
- Test: `tests/SunTools.Core.Tests/Geometry/ShadeMeshTests.cs`, `tests/SunTools.Core.Tests/Sunlit/PreparedSceneTests.cs`

**Interfaces:**
- Consumes: `Vec3`, `Point2`, `Window` (`ToLocal`, `Outline`, `Scale`), `ConvexClip.KeepFront`.
- Produces: `new ShadeMesh(IReadOnlyList<Vec3> vertices, IReadOnlyList<int> triangles)` with `Vertices`, `Triangles` (flat, three indices per triangle), `TriangleCount`, static `ShadeMesh.Empty`. Internal `ClipperConvert.Scale = 1e9`, `ClipperConvert.ToPath64(IReadOnlyList<Point2>) : Path64`, `ClipperConvert.ToPoint2(Point64) : Point2`. Internal `PreparedScene.Build(Window, ShadeMesh)` with `Window`, `IReadOnlyList<Vec3[]> Pieces` (normalised local coordinates, convex, `Z ≥ 0`), `int DegenerateTriangleCount`, `Path64 WindowPath`, `double WindowPathArea`. Test helpers `Quad(a,b,c,d)`, `Plate(x0,y0,x1,y1,z)`, `Merge(params ShadeMesh[])`, `Sun(x,y,z)` (normalised).

- [ ] **Step 1: Add helpers and write the failing tests**

Add to `TestScenes` (inside the class):
```csharp
    public static ShadeMesh Quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d) =>
        new ShadeMesh(new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 });

    /// <summary>Axis-aligned plate [x0,x1]×[y0,y1] parallel to the z = 0 window, at height z.</summary>
    public static ShadeMesh Plate(double x0, double y0, double x1, double y1, double z) =>
        Quad(V(x0, y0, z), V(x1, y0, z), V(x1, y1, z), V(x0, y1, z));

    public static ShadeMesh Merge(params ShadeMesh[] parts)
    {
        var vertices = new List<Vec3>();
        var triangles = new List<int>();
        foreach (var part in parts)
        {
            int offset = vertices.Count;
            vertices.AddRange(part.Vertices);
            foreach (int t in part.Triangles) triangles.Add(t + offset);
        }
        return new ShadeMesh(vertices, triangles);
    }

    public static Vec3 Sun(double x, double y, double z) => V(x, y, z).Normalized();
```

`tests/SunTools.Core.Tests/Geometry/ShadeMeshTests.cs`:
```csharp
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
```

`tests/SunTools.Core.Tests/Sunlit/PreparedSceneTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~ShadeMeshTests|FullyQualifiedName~PreparedSceneTests"`
Expected: build FAILS with `The type or namespace name 'ShadeMesh' could not be found`.

- [ ] **Step 3: Implement `ShadeMesh`, `ClipperConvert`, `PreparedScene`**

`src/SunTools.Core/Geometry/ShadeMesh.cs`:
```csharp
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
```

`src/SunTools.Core/Sunlit/ClipperConvert.cs`:
```csharp
using System;
using System.Collections.Generic;
using Clipper2Lib;
using SunTools.Core.Geometry;

namespace SunTools.Core.Sunlit;

/// <summary>Conversion between normalised window coordinates and Clipper2 integers.</summary>
internal static class ClipperConvert
{
    /// <summary>Integer units per normalised unit (window ≈ 1 × 1 → 1e-9 resolution).</summary>
    public const double Scale = 1e9;

    public static Path64 ToPath64(IReadOnlyList<Point2> poly)
    {
        var path = new Path64(poly.Count);
        foreach (var p in poly)
            path.Add(new Point64((long)Math.Round(p.X * Scale), (long)Math.Round(p.Y * Scale)));
        return path;
    }

    public static Point2 ToPoint2(Point64 p) => new Point2(p.X / Scale, p.Y / Scale);
}
```

`src/SunTools.Core/Sunlit/PreparedScene.cs`:
```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~ShadeMeshTests|FullyQualifiedName~PreparedSceneTests"`
Expected: `Passed!  - Failed: 0, Passed: 9`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add ShadeMesh and per-scene front clipping

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 5: `SunlitEngine.ComputeHour` with analytic cases

**Files:**
- Create: `src/SunTools.Core/Sunlit/HourResult.cs`, `src/SunTools.Core/Sunlit/SunlitEngine.cs`
- Modify: `tests/SunTools.Core.Tests/TestScenes.cs` (add `SignedArea`)
- Test: `tests/SunTools.Core.Tests/Sunlit/AnalyticCaseTests.cs`

**Interfaces:**
- Consumes: `Window`, `ShadeMesh`, `PreparedScene`, `ConvexClip.ClipToRect`, `ClipperConvert`.
- Produces: `SunlitEngine.GrazingCosine = 1e-9`. `SunlitEngine.ComputeHour(Window window, ShadeMesh shade, Vec3 sun, double dni, bool includePolygons = false) : HourResult`. Internal `SunlitEngine.ComputeHour(PreparedScene scene, Vec3 unitSun, double dni, bool includePolygons)`, `SunlitEngine.ValidateSun(Vec3, int index) : Vec3` (unit), `SunlitEngine.ValidateDni(double, int index)`. `HourResult`: `double SunlitFraction`, `double DirectIrradiance`, `IReadOnlyList<IReadOnlyList<Vec3>> SunlitPolygons` (world coordinates, empty unless requested). Test helper `TestScenes.SignedArea(IReadOnlyList<Vec3> poly, Vec3 normal)`.

- [ ] **Step 1: Add helper and write the failing tests**

Add to `TestScenes` (inside the class):
```csharp
    /// <summary>Area of a planar 3D polygon, signed along <paramref name="normal"/>.</summary>
    public static double SignedArea(IReadOnlyList<Vec3> poly, Vec3 normal)
    {
        var acc = new Vec3(0, 0, 0);
        for (int i = 1; i + 1 < poly.Count; i++)
            acc = acc + (poly[i] - poly[0]).Cross(poly[i + 1] - poly[0]);
        return acc.Dot(normal) / 2;
    }
```

`tests/SunTools.Core.Tests/Sunlit/AnalyticCaseTests.cs`:
```csharp
using System;
using System.Linq;
using SunTools.Core.Geometry;
using SunTools.Core.Sunlit;
using Xunit;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Sunlit;

/// <summary>Closed-form cases; tolerance 1e-6 (spec §7.1). Window in z = 0, normal +Z, "up" = +Y.</summary>
public class AnalyticCaseTests
{
    private const double Tol = 1e-6;

    private static double F(Window w, ShadeMesh s, Vec3 sun) => SunlitEngine.ComputeHour(w, s, sun, 0).SunlitFraction;

    private static ShadeMesh Overhang(double depth, double scale = 1, Vec3 offset = default) =>
        Quad(offset + V(-1000, 1, 0) * scale, offset + V(1002, 1, 0) * scale,
             offset + V(1002, 1, depth) * scale, offset + V(-1000, 1, depth) * scale);

    [Fact]
    public void No_shade_gives_full_sun_and_cosine_irradiance()
    {
        var r = SunlitEngine.ComputeHour(RectWindow(1, 1), ShadeMesh.Empty, Sun(0, 1, 1), 800);
        Close(1, r.SunlitFraction, Tol);
        Close(800 / Math.Sqrt(2), r.DirectIrradiance, 1e-9);
    }

    [Theory]
    [InlineData(0, 0, -1)]
    [InlineData(1, 0, 0)]
    public void Sun_behind_or_parallel_to_the_glass_gives_zero(double x, double y, double z)
    {
        var r = SunlitEngine.ComputeHour(RectWindow(1, 1), ShadeMesh.Empty, V(x, y, z), 800);
        Assert.Equal(0, r.SunlitFraction);
        Assert.Equal(0, r.DirectIrradiance);
    }

    [Fact]
    public void Large_plate_in_front_covers_the_window()
    {
        Close(0, F(RectWindow(1, 1), Plate(-10, -10, 10, 10, 0.5), Sun(0, 0, 1)), Tol);
        Close(0, F(RectWindow(1, 1), Plate(-10, -10, 10, 10, 0.5), Sun(0.3, 0.2, 1)), Tol);
    }

    [Fact]
    public void Plate_behind_the_glass_casts_no_shadow()
    {
        Close(1, F(RectWindow(1, 1), Plate(-10, -10, 10, 10, -0.5), Sun(0, 0, 1)), Tol);
    }

    [Fact]
    public void Glass_surface_passed_as_shade_casts_no_shadow()
    {
        Close(1, F(RectWindow(1, 1), Plate(0, 0, 1, 1, 0), Sun(0.2, 0.3, 1)), Tol);
    }

    [Theory]
    [InlineData(0.4, 0.8)] // shadow depth 0.4 · 0.5 = 0.2 of a 1 m high window
    [InlineData(3.0, 0.0)] // shadow deeper than the window
    public void Infinite_overhang_matches_closed_form(double depth, double expected)
    {
        Close(expected, F(RectWindow(2, 1), Overhang(depth), Sun(0.1, 0.5, 1)), Tol);
    }

    [Fact]
    public void Infinite_fin_matches_closed_form()
    {
        var fin = Quad(V(2, -1000, 0), V(2, 1001, 0), V(2, 1001, 0.5), V(2, -1000, 0.5));
        // shadow width 0.5 · 0.6 = 0.3 of a 2 m wide window
        Close(0.85, F(RectWindow(2, 1), fin, Sun(0.6, 0.1, 1)), Tol);
    }

    [Fact]
    public void Concave_L_plate_casts_an_exact_L_shadow()
    {
        var pts = new[] { V(0, 0, 0.3), V(1, 0, 0.3), V(1, 0.5, 0.3), V(0.5, 0.5, 0.3), V(0.5, 1, 0.3), V(0, 1, 0.3) };
        var l = new ShadeMesh(pts, new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 });
        Close(0.25, F(RectWindow(1, 1), l, Sun(0, 0, 1)), Tol);
        // shifted by −0.06 in x: shaded 0.94·0.5 + 0.44·0.5 = 0.69
        Close(0.31, F(RectWindow(1, 1), l, Sun(0.2, 0, 1)), Tol);
    }

    [Fact]
    public void Frame_with_opening_equal_to_the_window()
    {
        var frame = Merge(Plate(-1, -1, 2, 0, 0.2), Plate(-1, 1, 2, 2, 0.2), Plate(-1, 0, 0, 1, 0.2), Plate(1, 0, 2, 1, 0.2));
        Close(1, F(RectWindow(1, 1), frame, Sun(0, 0, 1)), Tol);
        Close(0.95, F(RectWindow(1, 1), frame, Sun(0.25, 0, 1)), Tol);
    }

    [Fact]
    public void Duplicated_and_reversed_triangles_are_counted_once()
    {
        var reversed = Quad(V(0, 0, 0.3), V(0, 1, 0.3), V(0.5, 1, 0.3), V(0.5, 0, 0.3));
        var shade = Merge(Plate(0, 0, 0.5, 1, 0.3), Plate(0, 0, 0.5, 1, 0.3), reversed);
        Close(0.5, F(RectWindow(1, 1), shade, Sun(0, 0, 1)), Tol);
    }

    [Fact]
    public void Nearly_grazing_sun_does_not_overflow()
    {
        double f = F(RectWindow(1, 1), Plate(-10, -10, 10, 10, 0.5), Sun(1, 0, 1e-6));
        Close(1, f, Tol);
    }

    [Fact]
    public void Georeferenced_coordinates_give_the_same_fraction()
    {
        var o = V(500_000, 4_000_000, 120);
        var window = Window.Create(new[] { o + V(0, 0, 0), o + V(2, 0, 0), o + V(2, 1, 0), o + V(0, 1, 0) });
        Close(0.8, F(window, Overhang(0.4, 1, o), Sun(0.1, 0.5, 1)), Tol);
    }

    [Fact]
    public void Centimetre_window_gives_the_same_fraction()
    {
        var window = Window.Create(new[] { V(0, 0, 0), V(0.02, 0, 0), V(0.02, 0.01, 0), V(0, 0.01, 0) });
        Close(0.8, F(window, Overhang(0.4, 0.01), Sun(0.1, 0.5, 1)), Tol);
    }

    [Fact]
    public void Concave_window_without_shade_is_fully_sunlit()
    {
        var window = Window.Create(new[] { V(0, 0, 0), V(1, 0, 0), V(1, 0.5, 0), V(0.5, 0.5, 0), V(0.5, 1, 0), V(0, 1, 0) });
        Close(1, F(window, ShadeMesh.Empty, Sun(0.3, 0.3, 1)), Tol);
    }

    [Fact]
    public void Non_unit_sun_vector_is_normalised()
    {
        var a = SunlitEngine.ComputeHour(RectWindow(2, 1), Overhang(0.4), Sun(0.1, 0.5, 1), 700);
        var b = SunlitEngine.ComputeHour(RectWindow(2, 1), Overhang(0.4), Sun(0.1, 0.5, 1) * 5, 700);
        Close(a.SunlitFraction, b.SunlitFraction, 1e-12);
        Close(a.DirectIrradiance, b.DirectIrradiance, 1e-9);
    }

    [Theory]
    [InlineData(0, 0, 0, 100)]
    [InlineData(double.NaN, 0, 1, 100)]
    [InlineData(0, 0, 1, -1)]
    [InlineData(0, 0, 1, double.NaN)]
    public void Invalid_sun_or_dni_is_rejected(double x, double y, double z, double dni)
    {
        Assert.Throws<ArgumentException>(() => SunlitEngine.ComputeHour(RectWindow(1, 1), ShadeMesh.Empty, V(x, y, z), dni));
    }

    [Fact]
    public void Sunlit_polygons_are_empty_by_default_and_match_the_fraction_when_requested()
    {
        var window = RectWindow(2, 1);
        Assert.Empty(SunlitEngine.ComputeHour(window, Overhang(0.4), Sun(0.1, 0.5, 1), 0).SunlitPolygons);
        var r = SunlitEngine.ComputeHour(window, Overhang(0.4), Sun(0.1, 0.5, 1), 0, includePolygons: true);
        Assert.NotEmpty(r.SunlitPolygons);
        double area = Math.Abs(r.SunlitPolygons.Sum(p => SignedArea(p, window.Normal)));
        Close(r.SunlitFraction * window.Area, area, 1e-9);
        Assert.All(r.SunlitPolygons.SelectMany(p => p), p => Close(0, p.Z, 1e-9));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~AnalyticCaseTests"`
Expected: build FAILS with `The name 'SunlitEngine' does not exist in the current context`.

- [ ] **Step 3: Implement `HourResult` and `SunlitEngine.ComputeHour`**

`src/SunTools.Core/Sunlit/HourResult.cs`:
```csharp
using System;
using System.Collections.Generic;
using SunTools.Core.Geometry;

namespace SunTools.Core.Sunlit;

/// <summary>Direct-sun result for one window, one shade state and one hour.</summary>
public sealed class HourResult
{
    internal static readonly IReadOnlyList<IReadOnlyList<Vec3>> NoPolygons = Array.Empty<IReadOnlyList<Vec3>>();
    internal static readonly HourResult NoSun = new HourResult(0, 0, NoPolygons);

    internal HourResult(double sunlitFraction, double directIrradiance, IReadOnlyList<IReadOnlyList<Vec3>> sunlitPolygons)
    {
        SunlitFraction = sunlitFraction;
        DirectIrradiance = directIrradiance;
        SunlitPolygons = sunlitPolygons;
    }

    /// <summary>Sunlit area divided by window area, in [0, 1].</summary>
    public double SunlitFraction { get; }
    /// <summary>Direct (beam) irradiance on the glass in W/m²: f · DNI · cos(incidence).</summary>
    public double DirectIrradiance { get; }
    /// <summary>Sunlit regions on the glass in world coordinates (outer boundaries and holes with
    /// opposite orientation); empty unless requested. For preview only.</summary>
    public IReadOnlyList<IReadOnlyList<Vec3>> SunlitPolygons { get; }
}
```

`src/SunTools.Core/Sunlit/SunlitEngine.cs`:
```csharp
using System;
using System.Collections.Generic;
using Clipper2Lib;
using SunTools.Core.Geometry;

namespace SunTools.Core.Sunlit;

/// <summary>Exact sunlit fraction of a window behind any triangulated shade.</summary>
public static class SunlitEngine
{
    /// <summary>At or below this value of s·n the sun is behind or parallel to the glass.</summary>
    public const double GrazingCosine = 1e-9;

    /// <summary>Computes one hour.</summary>
    /// <param name="sun">Direction towards the sun (any non-zero length).</param>
    /// <param name="dni">Direct normal irradiance in W/m² (≥ 0).</param>
    /// <param name="includePolygons">Also return the sunlit polygons (preview).</param>
    public static HourResult ComputeHour(Window window, ShadeMesh shade, Vec3 sun, double dni, bool includePolygons = false)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (shade == null) throw new ArgumentNullException(nameof(shade));
        var unitSun = ValidateSun(sun, 0);
        ValidateDni(dni, 0);
        return ComputeHour(PreparedScene.Build(window, shade), unitSun, dni, includePolygons);
    }

    internal static HourResult ComputeHour(PreparedScene scene, Vec3 unitSun, double dni, bool includePolygons)
    {
        var w = scene.Window;
        double cos = unitSun.Dot(w.Normal);
        if (cos <= GrazingCosine)
            return HourResult.NoSun;

        // Projection along the sun onto the glass plane: P' = P − (P_w / s_w)·s.
        double du = unitSun.Dot(w.U) / cos;
        double dv = unitSun.Dot(w.V) / cos;
        var clip = new Paths64(scene.Pieces.Count);
        var projected = new List<Point2>(4);
        foreach (var piece in scene.Pieces)
        {
            projected.Clear();
            foreach (var p in piece)
                projected.Add(new Point2(p.X - p.Z * du, p.Y - p.Z * dv));
            var inside = ConvexClip.ClipToRect(projected, w.MinX, w.MinY, w.MaxX, w.MaxY);
            if (inside.Count >= 3)
                clip.Add(ClipperConvert.ToPath64(inside));
        }

        Paths64 sunlit;
        if (clip.Count == 0)
        {
            sunlit = new Paths64 { scene.WindowPath };
        }
        else
        {
            var clipper = new Clipper64();
            clipper.AddSubject(new Paths64 { scene.WindowPath });
            clipper.AddClip(clip);
            sunlit = new Paths64();
            clipper.Execute(ClipType.Difference, FillRule.NonZero, sunlit);
        }

        double f = Math.Min(1, Math.Abs(Clipper.Area(sunlit)) / scene.WindowPathArea);
        var polygons = includePolygons ? ToWorld(w, sunlit) : HourResult.NoPolygons;
        return new HourResult(f, f * dni * cos, polygons);
    }

    internal static Vec3 ValidateSun(Vec3 sun, int index)
    {
        if (!sun.IsFinite || !(sun.Length > 1e-12))
            throw new ArgumentException($"Sun vector at index {index} is zero or not finite.", nameof(sun));
        return sun.Normalized();
    }

    internal static void ValidateDni(double dni, int index)
    {
        if (!Vec3.IsFiniteNumber(dni) || dni < 0)
            throw new ArgumentException($"DNI at index {index} is negative or not finite.", nameof(dni));
    }

    private static IReadOnlyList<IReadOnlyList<Vec3>> ToWorld(Window w, Paths64 paths)
    {
        var result = new List<IReadOnlyList<Vec3>>(paths.Count);
        foreach (var path in paths)
        {
            var poly = new Vec3[path.Count];
            for (int i = 0; i < path.Count; i++)
                poly[i] = w.ToWorld(ClipperConvert.ToPoint2(path[i]));
            result.Add(poly);
        }
        return result;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~AnalyticCaseTests"`
Expected: `Passed!  - Failed: 0, Passed: 22`.
If a polygon-area assertion fails only by sign, the `Math.Abs` around the sum is missing. Clipper2 returns outer and hole paths with opposite orientations.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add exact one-hour sunlit fraction with analytic cases

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 6: Multi-hour series in parallel

**Files:**
- Create: `src/SunTools.Core/Sunlit/SunlitSeries.cs`
- Modify: `src/SunTools.Core/Sunlit/SunlitEngine.cs` (add `Compute`)
- Test: `tests/SunTools.Core.Tests/Sunlit/SeriesTests.cs`

**Interfaces:**
- Consumes: `SunlitEngine.ComputeHour(PreparedScene, Vec3, double, bool)`, `ValidateSun`, `ValidateDni`, `PreparedScene.Build`.
- Produces: `SunlitEngine.Compute(Window window, ShadeMesh shade, IReadOnlyList<Vec3> suns, IReadOnlyList<double> dni, bool parallel = true) : SunlitSeries`. `SunlitSeries`: `IReadOnlyList<double> SunlitFraction`, `IReadOnlyList<double> DirectIrradiance`, `int DegenerateTriangleCount`, `int Count`.

- [ ] **Step 1: Write the failing tests**

`tests/SunTools.Core.Tests/Sunlit/SeriesTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~SeriesTests"`
Expected: build FAILS with `'SunlitEngine' does not contain a definition for 'Compute'`.

- [ ] **Step 3: Implement `SunlitSeries` and `Compute`**

`src/SunTools.Core/Sunlit/SunlitSeries.cs`:
```csharp
using System.Collections.Generic;

namespace SunTools.Core.Sunlit;

/// <summary>Direct-sun results for one window and one shade state over many hours.</summary>
public sealed class SunlitSeries
{
    internal SunlitSeries(double[] sunlitFraction, double[] directIrradiance, int degenerateTriangleCount)
    {
        SunlitFraction = sunlitFraction;
        DirectIrradiance = directIrradiance;
        DegenerateTriangleCount = degenerateTriangleCount;
    }

    public IReadOnlyList<double> SunlitFraction { get; }
    public IReadOnlyList<double> DirectIrradiance { get; }
    /// <summary>Zero-area shade triangles that were skipped.</summary>
    public int DegenerateTriangleCount { get; }
    public int Count => SunlitFraction.Count;
}
```

Add to `SunlitEngine` (and add `using System.Threading.Tasks;` at the top):
```csharp
    /// <summary>Computes every hour; the scene is prepared once.</summary>
    /// <param name="suns">Directions towards the sun, one per hour.</param>
    /// <param name="dni">Direct normal irradiance per hour in W/m².</param>
    /// <param name="parallel">Compute hours in parallel (results are identical either way).</param>
    public static SunlitSeries Compute(Window window, ShadeMesh shade, IReadOnlyList<Vec3> suns,
        IReadOnlyList<double> dni, bool parallel = true)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (shade == null) throw new ArgumentNullException(nameof(shade));
        if (suns == null) throw new ArgumentNullException(nameof(suns));
        if (dni == null) throw new ArgumentNullException(nameof(dni));
        if (suns.Count != dni.Count)
            throw new ArgumentException($"Got {suns.Count} sun vectors but {dni.Count} DNI values.", nameof(dni));

        var unitSuns = new Vec3[suns.Count];
        for (int i = 0; i < suns.Count; i++)
        {
            unitSuns[i] = ValidateSun(suns[i], i);
            ValidateDni(dni[i], i);
        }

        var scene = PreparedScene.Build(window, shade);
        var fraction = new double[unitSuns.Length];
        var irradiance = new double[unitSuns.Length];
        void One(int i)
        {
            var r = ComputeHour(scene, unitSuns[i], dni[i], includePolygons: false);
            fraction[i] = r.SunlitFraction;
            irradiance[i] = r.DirectIrradiance;
        }

        if (parallel)
            Parallel.For(0, unitSuns.Length, One);
        else
            for (int i = 0; i < unitSuns.Length; i++) One(i);

        return new SunlitSeries(fraction, irradiance, scene.DegenerateTriangleCount);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~SeriesTests"`
Expected: `Passed!  - Failed: 0, Passed: 6`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add parallel multi-hour SunlitEngine.Compute

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 7: Random scenes — invariance, independent area check, ray-casting oracle

**Files:**
- Create: `tests/SunTools.Core.Tests/Randomized/RigidTransform.cs`, `tests/SunTools.Core.Tests/Randomized/RandomScenes.cs`, `tests/SunTools.Core.Tests/Randomized/RayOracle.cs`
- Test: `tests/SunTools.Core.Tests/Randomized/InvarianceTests.cs`, `tests/SunTools.Core.Tests/Randomized/OracleTests.cs`

**Interfaces:**
- Consumes: `Window.Create`, `Window` public frame (`Origin`, `U`, `V`, `Normal`, `Vertices`, `Area`), `ShadeMesh`, `SunlitEngine.ComputeHour`, `TestScenes.Quad`, `Merge`, `RectWindow`, `Sun`, `V`.
- Produces (test-only): `RigidTransform.Random(Random rng, double maxTranslation)`, `Rotate(Vec3)`, `Apply(Vec3)`. `Scene` (`Window`, `Shade`, `Sun`, `Transformed(RigidTransform)`). `RandomScenes.Create(int seed, bool frontOnly = false) : Scene`, `RandomScenes.Seeds(int count)`. `RayOracle.SunlitFraction(Window, ShadeMesh, Vec3 sun, int n = 400) : double`.

- [ ] **Step 1: Write the generators and the oracle**

`tests/SunTools.Core.Tests/Randomized/RigidTransform.cs`:
```csharp
using System;
using SunTools.Core.Geometry;

namespace SunTools.Core.Tests.Randomized;

/// <summary>Rotation about a unit axis (Rodrigues) followed by a translation.</summary>
internal readonly struct RigidTransform
{
    private readonly Vec3 _axis;
    private readonly double _angle;
    private readonly Vec3 _translation;

    public RigidTransform(Vec3 axis, double angle, Vec3 translation)
    {
        _axis = axis.Normalized();
        _angle = angle;
        _translation = translation;
    }

    public Vec3 Rotate(Vec3 v)
    {
        double c = Math.Cos(_angle), s = Math.Sin(_angle);
        return v * c + _axis.Cross(v) * s + _axis * (_axis.Dot(v) * (1 - c));
    }

    public Vec3 Apply(Vec3 p) => Rotate(p) + _translation;

    public static RigidTransform Random(System.Random rng, double maxTranslation)
    {
        Vec3 axis;
        do
        {
            axis = new Vec3(2 * rng.NextDouble() - 1, 2 * rng.NextDouble() - 1, 2 * rng.NextDouble() - 1);
        } while (axis.Length < 0.1);
        double T() => (2 * rng.NextDouble() - 1) * maxTranslation;
        return new RigidTransform(axis, 2 * Math.PI * rng.NextDouble(), new Vec3(T(), T(), T()));
    }
}
```

`tests/SunTools.Core.Tests/Randomized/RandomScenes.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SunTools.Core.Geometry;
using static SunTools.Core.Tests.TestScenes;

namespace SunTools.Core.Tests.Randomized;

internal sealed class Scene
{
    public Scene(Window window, ShadeMesh shade, Vec3 sun)
    {
        Window = window;
        Shade = shade;
        Sun = sun;
    }

    public Window Window { get; }
    public ShadeMesh Shade { get; }
    public Vec3 Sun { get; }

    public Scene Transformed(RigidTransform t) => new Scene(
        Window.Create(Window.Vertices.Select(t.Apply).ToArray(), t.Rotate(Window.Normal)),
        new ShadeMesh(Shade.Vertices.Select(t.Apply).ToArray(), Shade.Triangles),
        t.Rotate(Sun));
}

/// <summary>Deterministic random scenes: rectangular window, one of three shade kinds, random sun
/// in front of the glass, then a random rigid motion.</summary>
internal static class RandomScenes
{
    public static IEnumerable<object[]> Seeds(int count) => Enumerable.Range(0, count).Select(i => new object[] { i });

    /// <param name="frontOnly">Keep all shade geometry strictly in front of the glass.</param>
    public static Scene Create(int seed, bool frontOnly = false)
    {
        var rng = new System.Random(seed);
        double w = 1 + 2 * rng.NextDouble();
        double h = 1 + 2 * rng.NextDouble();
        var window = Window.Create(new[] { V(0, 0, 0), V(w, 0, 0), V(w, h, 0), V(0, h, 0) });
        var shade = (seed % 3) switch
        {
            0 => RandomTriangles(rng, w, h, frontOnly ? 0.05 : -0.5),
            1 => Slats(rng, w, h),
            _ => LPlate(rng, w, h),
        };
        double sz = 0.15 + 0.85 * rng.NextDouble();
        double phi = 2 * Math.PI * rng.NextDouble();
        double r = Math.Sqrt(1 - sz * sz);
        var local = new Scene(window, shade, V(r * Math.Cos(phi), r * Math.Sin(phi), sz));
        return local.Transformed(RigidTransform.Random(rng, 100));
    }

    private static ShadeMesh RandomTriangles(System.Random rng, double w, double h, double zMin)
    {
        int n = 1 + rng.Next(20);
        var vertices = new List<Vec3>();
        var triangles = new List<int>();
        for (int i = 0; i < 3 * n; i++)
        {
            vertices.Add(V(-1 + (w + 2) * rng.NextDouble(), -1 + (h + 2) * rng.NextDouble(), zMin + (1.5 - zMin) * rng.NextDouble()));
            triangles.Add(i);
        }
        return new ShadeMesh(vertices, triangles);
    }

    private static ShadeMesh Slats(System.Random rng, double w, double h)
    {
        int k = 2 + rng.Next(7);
        double depth = 0.1 + 0.2 * rng.NextDouble();
        double tilt = (rng.NextDouble() * 120 - 60) * Math.PI / 180;
        double dy = depth * Math.Sin(tilt), dz = depth * Math.Cos(tilt), z0 = 0.05;
        var slats = new List<ShadeMesh>();
        for (int i = 0; i < k; i++)
        {
            double y = (i + 0.5) * h / k;
            slats.Add(Quad(V(-0.5, y, z0), V(w + 0.5, y, z0), V(w + 0.5, y + dy, z0 + dz), V(-0.5, y + dy, z0 + dz)));
        }
        return Merge(slats.ToArray());
    }

    private static ShadeMesh LPlate(System.Random rng, double w, double h)
    {
        double z = 0.1 + 0.9 * rng.NextDouble();
        double x0 = -0.5 + rng.NextDouble(), y0 = -0.5 + rng.NextDouble();
        double a = 0.5 + w * rng.NextDouble(), b = 0.5 + h * rng.NextDouble();
        double cut = 0.3 + 0.4 * rng.NextDouble();
        var pts = new[]
        {
            V(x0, y0, z), V(x0 + a, y0, z), V(x0 + a, y0 + cut * b, z),
            V(x0 + cut * a, y0 + cut * b, z), V(x0 + cut * a, y0 + b, z), V(x0, y0 + b, z),
        };
        // Fan from vertex 0, which sees the whole L (the reflex vertex is 3).
        return new ShadeMesh(pts, new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 });
    }
}
```

`tests/SunTools.Core.Tests/Randomized/RayOracle.cs`:
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using SunTools.Core.Geometry;

namespace SunTools.Core.Tests.Randomized;

/// <summary>Brute-force reference: an n × n grid of points on the window, one ray towards the sun
/// per point, tested against every triangle (Möller–Trumbore). Slow but obviously correct.</summary>
internal static class RayOracle
{
    public static double SunlitFraction(Window window, ShadeMesh shade, Vec3 sun, int n = 400)
    {
        var s = sun.Normalized();
        if (s.Dot(window.Normal) <= 1e-9) return 0;

        var outline = window.Vertices
            .Select(p => { var d = p - window.Origin; return (X: d.Dot(window.U), Y: d.Dot(window.V)); })
            .ToArray();
        double minX = outline.Min(p => p.X), maxX = outline.Max(p => p.X);
        double minY = outline.Min(p => p.Y), maxY = outline.Max(p => p.Y);
        var triangles = Enumerable.Range(0, shade.TriangleCount)
            .Select(t => (A: shade.Vertices[shade.Triangles[3 * t]], B: shade.Vertices[shade.Triangles[3 * t + 1]], C: shade.Vertices[shade.Triangles[3 * t + 2]]))
            .ToArray();

        var inside = new int[n];
        var lit = new int[n];
        Parallel.For(0, n, j =>
        {
            double y = minY + (j + 0.5) * (maxY - minY) / n;
            for (int i = 0; i < n; i++)
            {
                double x = minX + (i + 0.5) * (maxX - minX) / n;
                if (!Contains(outline, x, y)) continue;
                inside[j]++;
                var origin = window.Origin + window.U * x + window.V * y;
                bool blocked = false;
                for (int k = 0; k < triangles.Length && !blocked; k++)
                    blocked = Hits(origin, s, triangles[k].A, triangles[k].B, triangles[k].C);
                if (!blocked) lit[j]++;
            }
        });
        return (double)lit.Sum() / inside.Sum();
    }

    private static bool Hits(Vec3 origin, Vec3 dir, Vec3 a, Vec3 b, Vec3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = dir.Cross(e2);
        double det = e1.Dot(p);
        if (Math.Abs(det) < 1e-12) return false;
        double inv = 1 / det;
        var tv = origin - a;
        double u = tv.Dot(p) * inv;
        if (u < 0 || u > 1) return false;
        var q = tv.Cross(e1);
        double v = dir.Dot(q) * inv;
        if (v < 0 || u + v > 1) return false;
        return e2.Dot(q) * inv > 1e-9;
    }

    private static bool Contains((double X, double Y)[] poly, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > y) != (poly[j].Y > y)
                && x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }
}
```

- [ ] **Step 2: Write the tests**

`tests/SunTools.Core.Tests/Randomized/InvarianceTests.cs`:
```csharp
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
            clip.Add(new PathD { Project(v[t[i]]), Project(v[t[i + 1]]), Project(v[t[i + 2]]) });
        double shaded = Math.Abs(Clipper.Area(Clipper.Intersect(subject, clip, FillRule.NonZero, 8)));

        double sunlit = f * window.Area;
        Assert.True(Math.Abs(sunlit + shaded - window.Area) < 1e-6 * window.Area,
            $"seed {seed}: sunlit {sunlit:R} + shaded {shaded:R} != window {window.Area:R}");
    }
}
```

`tests/SunTools.Core.Tests/Randomized/OracleTests.cs`:
```csharp
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
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test SunTools.sln --filter "FullyQualifiedName~SunTools.Core.Tests.Randomized"`
Expected: `Passed!  - Failed: 0, Passed: 701` (100 + 100 + 1 + 500), in under two minutes.
The engine was written in Tasks 4–6, so these tests are expected to pass on the first run. A failure is a real defect. Rerun the failing seed alone, reduce the scene (fewer triangles), and fix the engine, not the tolerance. Use superpowers:systematic-debugging.

- [ ] **Step 4: Run the whole suite**

Run: `dotnet test SunTools.sln -c Release`
Expected: `Passed!  - Failed: 0, Passed: 761` (4 + 6 + 13 + 9 + 22 + 6 + 701).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add invariance, independent-area and ray-casting oracle tests

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

---

### Task 8: Continuous integration on Windows and macOS

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `SunTools.sln`, `global.json`.
- Produces: a `ci` workflow, jobs `test (windows-latest)` and `test (macos-latest)`.

- [ ] **Step 1: Write the workflow**

`.github/workflows/ci.yml`:
```yaml
name: ci

on:
  push:
  pull_request:

jobs:
  test:
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Test
        run: dotnet test SunTools.sln -c Release
```

- [ ] **Step 2: Check it locally**

Run: `dotnet test SunTools.sln -c Release`
Expected: `Passed!  - Failed: 0, Passed: 761`.
Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('ok')"`. If PyYAML is not installed, `ruby -ryaml -e "YAML.load_file('.github/workflows/ci.yml'); puts 'ok'"`.
Expected: `ok`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "Add CI: build and test on Windows and macOS

Claude-Session: https://claude.ai/code/session_014wMfTYKspF9XcTtr37vivK"
```

- [ ] **Step 4: Hand off for the remote run**

Pushing the branch, pushing the tag `v0-2017` and opening the PR are outward-facing. Ask the owner before doing any of them (superpowers:finishing-a-development-branch). Once pushed, both matrix jobs must be green before merging.
