# SuntoolGH

[![ci](https://github.com/peitra/SuntoolGH/actions/workflows/ci.yml/badge.svg)](https://github.com/peitra/SuntoolGH/actions/workflows/ci.yml)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)](LICENSE.md)

SuntoolGH evaluates the direct-sun performance of **actuated shading devices**: awnings,
venetian blinds, louvres, trackers, or any shade whose geometry changes with its
actuation state. For a window, a shade state and an hour it computes:

- the **sunlit fraction** `f` of the window (0 to 1);
- the **direct irradiance on the glass** `E_dir` in W/m².

The goal is to make it fast enough to sweep the whole actuation space hour by hour, and
to export the results as one table (state × hour × metric) for analysis outside
Grasshopper.

> **Status: work in progress. There is no Grasshopper plugin to install yet.**
> The repository currently contains the computation engine (`SunTools.Core`), a .NET
> library with no Rhino dependency, and its test suite. The Grasshopper components come
> in a later step (see [Roadmap](#roadmap)).

## How the engine works

The engine works with any triangulated shade. It makes no assumption about the shade
type.

1. Triangles behind the glass plane are clipped away, since they cannot shade the
   window.
2. Each remaining triangle is projected along the sun direction onto the glass plane.
   A projected triangle is always a convex polygon of at most four vertices.
3. The projections are clipped to the window's bounding box.
4. One boolean difference, *window minus projected shadows*, gives the sunlit area. It
   uses [Clipper2](https://github.com/AngusJohnson/Clipper2) in 64-bit integer
   coordinates. All shape complexity (concave shades, holes, overlaps, many slats) is
   resolved in this single step.
5. `f = A_sunlit / A_window` and `E_dir = f · DNI · cos θ`, where θ is the angle of
   incidence on the glass.

When the sun is behind or parallel to the glass, the result is `f = 0` and
`E_dir = 0`. Hours are independent and are
computed in parallel. Curved shades are approximated by their mesh.

The full design is in
[`docs/design/2026-10-15-suntools-v1-design.md`](docs/design/2026-10-15-suntools-v1-design.md).

## Using the Core library

`SunTools.Core` targets `netstandard2.0`. Its only dependency is the `Clipper2` NuGet
package. Units are metres, and sun vectors point **towards** the sun.

```csharp
using SunTools.Core.Geometry;
using SunTools.Core.Sunlit;

// A 1 m × 1 m window in the plane y = 0. The outward normal follows the right-hand
// rule on the vertex order (here +Y), or can be passed as a second argument.
var window = Window.Create(new[]
{
    new Vec3(0, 0, 0), new Vec3(0, 0, 1), new Vec3(1, 0, 1), new Vec3(1, 0, 0),
});

// A 0.5 m deep overhang along the top edge, as two triangles.
var shade = new ShadeMesh(
    new[] { new Vec3(0, 0, 1), new Vec3(1, 0, 1), new Vec3(1, 0.5, 1), new Vec3(0, 0.5, 1) },
    new[] { 0, 1, 2, 0, 2, 3 });

// Sun at 45° in front of the window: the overhang shades the top half.
HourResult r = SunlitEngine.ComputeHour(window, shade, sun: new Vec3(0, 1, 1), dni: 800);
// r.SunlitFraction == 0.5, r.DirectIrradiance ≈ 283 W/m² (0.5 · 800 · cos 45°)

// Many hours at once (the scene is prepared once, hours run in parallel):
SunlitSeries s = SunlitEngine.Compute(window, shade, suns, dniPerHour);
```

Invalid input (a non-planar or self-intersecting window, a zero sun vector, negative
DNI) raises an `ArgumentException` with an explicit message. Zero-area shade triangles
are skipped and counted in `SunlitSeries.DegenerateTriangleCount`.

## Build and test

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Then run:

```sh
dotnet test SunTools.sln
```

CI runs `dotnet test SunTools.sln -c Release` on Windows and macOS on every push and
pull request.

## How the results are checked

- **Analytic cases**, checked to within 1e-6: an overhang and a fin against their
  closed-form solutions, no shade, full cover, sun parallel to the glass, a shade
  entirely behind the glass, an L-shaped concave shade, and a frame whose opening
  matches the window.
- **Invariants on random scenes**: sunlit area plus shaded area equals window area.
  Moving or rotating the whole scene together with the sun leaves the result
  unchanged.
- **Ray-casting reference**: about 500 random scenes (random triangles, concave meshes,
  slats, geometry on both sides of the glass) are compared with a brute-force ray cast
  on a grid over the window, to within 0.01. Seeds are fixed, and a failure prints its
  seed.
- **Edge cases typical of real models**: the glass itself passed as shade geometry, closed polylines that repeat their first vertex, near-grazing suns,
  duplicated or reversed triangles, georeferenced coordinates (≈ 4,000 km from the
  origin), and windows of 1 cm.

Validation against EnergyPlus has **not been done yet**. It is planned, see below.

## Roadmap

| Step | Content | State |
|---|---|---|
| 1 | Core sunlit-fraction engine and its tests | done |
| 2 | Solar position: EPW weather-file reader, NREL SPA | planned |
| 3 | Grasshopper plugin for Rhino 8 (Windows and macOS): Sun Path, Shade States, Sunlit Analysis, Export Table | planned |
| 4 | Validation against EnergyPlus (PixelCounting) on overhang, fin, concave and multi-slat cases and on awning, venetian and two-axis tracker examples. Target: p95 of \|f − f_EnergyPlus\| ≤ 0.02 | planned |

Out of scope for v1: diffuse light, inter-reflections, illuminance and thermal loads.
Ladybug Tools / Honeybee can be used to cross-check these, but they are not part of the
engine.

## History

SuntoolGH started in 2017 as a set of Grasshopper tools for research on actuated shading.
That code is kept unchanged on the `develop` branch. It is not
maintained and, as its own README states, not functional. The `master` branch is a rewrite started in
October 2026.

## License and contact

GPL-3.0, see [`LICENSE.md`](LICENSE.md).

Maintained by Victor Charpentier, [PEITRA](https://peitra.eu).
Contact: victor.charpentier@peitra.eu
