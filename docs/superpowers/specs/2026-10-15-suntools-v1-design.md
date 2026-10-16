# SunTools v1 — Design Spec

**Date:** 2026-10-15
**Status:** draft, awaiting review
**Scope:** v1 of SuntoolGH: a validated direct-sun engine and a minimal Grasshopper plugin.

## 1. Goal

An open-source Grasshopper plugin (Rhino 8, macOS and Windows) that evaluates the
direct-sun performance of **any actuated shading geometry**, fast enough to sweep the
whole actuation space hour by hour, and exports the results as a long-format table
(state × hour × metric).

v1 outputs, per window, per shade state and per hour:

- **sunlit fraction** `f` of the window (0–1);
- **direct irradiance on the glass** `E_dir` in W/m².

### Non-goals (v1)

- Daylight or thermal physics (diffuse light, inter-reflections, illuminance, loads).
  Ladybug Tools / Honeybee may be used to cross-check, never inside the engine.
- Windows with holes or mullions (one call per glazed polygon instead).
- Automatic slider driving and ready-made typology components (awning, venetian,
  tracker). The typologies ship as example `.gh` files built with the generic components.
- Coplanar-face merging and other mesh optimisations, unless a benchmark requires them.

### Success criterion

Validated against EnergyPlus (PixelCounting shading) on overhang, fin, concave and
multi-slat cases and on three rebuilt typologies (awning, venetian, two-axis tracker):
**p95 of |f_engine − f_EnergyPlus| ≤ 0.02** over every sunny hour of every case
(section 7.2). Analytic cases must pass within 1e-6 first.

## 2. Repository layout

The 2017 code is frozen under the tag `v0-2017` (and the existing `develop` branch) and
removed from `master`. The new solution:

```
SuntoolGH/
├─ src/
│  ├─ SunTools.Core/        netstandard2.0; only dependency: Clipper2 (NuGet)
│  │   ├─ Geometry/         Vec3, window local frame, triangle soup
│  │   ├─ Sunlit/           sunlit-fraction engine
│  │   └─ Solar/            EPW reader, NREL SPA, direct irradiance
│  └─ SunTools.GH/          Grasshopper plugin (.gha), multi-target net48;net7.0
├─ tests/
│  └─ SunTools.Core.Tests/  xUnit, net8.0, no Rhino dependency
├─ validation/
│  ├─ SunTools.Validate/    net8.0 console: scene JSON + EnergyPlus CSV → metrics JSON
│  ├─ cases/                scene JSON files
│  └─ energyplus/           IDF generator (Python) and run scripts
├─ examples/                .gh example files (the three typologies)
└─ .github/workflows/       CI: build + Core tests on windows-latest and macos-latest
```

Rules:

- **The Core knows nothing about Rhino.** It takes and returns plain arrays.
- **The plugin computes nothing.** It converts Rhino geometry to Core types, calls the
  Core and formats outputs. Any wrong number is reproducible as a Core test.
- `bin/`, `obj/`, `.vs/` and `packages/` are git-ignored; dependencies come from NuGet.

## 3. Core algorithm

Inputs for one window, one shade state, one hour:

- window: planar simple polygon, outward normal **n**;
- shade: triangle soup in world coordinates (metres);
- sun vector **s** (unit, pointing towards the sun) and DNI (W/m²).

Steps:

0. **Sun behind or grazing the glass:** if `s·n ≤ 1e-9`, return `f = 0`, `E_dir = 0`
   (EnergyPlus convention).
1. **Window local frame:** origin at the window centroid, `u, v` in-plane, `w = n`.
   Lengths are normalised by the window's largest dimension (window ≈ 1 × 1).
2. **Clip each triangle to the half-space `w > 0`** (outside of the glass plane).
   Geometry behind the glass cannot shade it. A clipped triangle is a triangle or a
   convex quadrilateral.
3. **Project along the sun onto `w = 0`:** `P' = P − (P_w / s_w)·s`. The image of a
   convex polygon under this affine map is a convex polygon.
4. **Clip to the window bounding box** in double precision (the same convex
   Sutherland–Hodgman routine as step 2, applied to four half-planes; Clipper2's
   `RectClip` on `PathsD` is capped at 8 decimals) and drop empty results. This bounds
   coordinates before integer conversion (grazing suns produce very large projections)
   and culls far geometry.
5. **One boolean operation:** `Difference(window, projected polygons)` with the
   *NonZero* fill rule, in Clipper2 64-bit integers at scale 1e9 of the normalised
   frame. Every projected polygon is first oriented counter-clockwise: NonZero sums
   signed winding numbers, so a clockwise piece over a counter-clockwise one would
   cancel its shadow. With consistent orientation the union is implicit and the input
   triangle winding is irrelevant. Result: sunlit area `A_s`.
6. **Outputs:** `f = A_s / A_window`; `E_dir = f · DNI · (s·n)`.

Hours are independent and computed in parallel (`Parallel.For`). On request the Core
also returns the sunlit polygons (for preview only).

Why any shade works: every input geometry is reduced to triangles; each triangle only
ever becomes a convex polygon of at most four vertices; all shape complexity
(concavity, holes, overlaps, thousands of slats) is resolved by the single Clipper2
boolean. Curved shades are approximated by their mesh; the error is controlled by the
meshing parameters exposed in the plugin.

## 4. Grasshopper components

1. **Sun Path** — inputs: EPW file, north vector (default **+Y**), optional hour filter.
   Outputs: sun vectors, DNI, hour of year (HOY). Uses the built-in EPW reader and
   NREL SPA. Ladybug sun vectors and DNI can be plugged into Sunlit Analysis instead.
2. **Shade States** — inputs: a tree of rigid parts (one branch per moving part), one
   rotation or translation axis per part, a value list per degree of freedom.
   Outputs: tree `{state}` of geometry and a state table (DOF values per state).
   Any other definition may build the `{state}` tree itself instead (free path), e.g.
   flexible shades whose deformed shapes come from a mechanics model and are imported
   as meshes.
3. **Sunlit Analysis** — inputs: window(s) (planar curve or surface), `{state}` tree,
   sun vectors, DNI, HOY, meshing parameters, optional window normal, optional
   sun-vector direction override. Outputs: trees `{state; window}` holding hourly lists
   of `f` and `E_dir`; preview of sunlit polygons for a selected state and hour; window
   normal arrows. Meshing happens once per state; this is the only Rhino-dependent step.
4. **Export Table** — writes a long-format CSV:
   `state, dof_1, …, dof_k, window, hoy, metric, value`.

Units: the plugin converts Rhino model units to metres before calling the Core.

## 5. Data formats

**Scene JSON** (validation and Core tests):

```json
{
  "window": { "vertices": [[x, y, z], ...], "normal": [nx, ny, nz] },
  "shade":  { "vertices": [[x, y, z], ...], "triangles": [[i, j, k], ...] },
  "hours":  [{ "hoy": 0, "sun": [sx, sy, sz], "dni": 0.0 }, ...]
}
```

Metres; `sun` points towards the sun; `normal` is optional (right-hand rule on the
vertex order otherwise).

**Export CSV:** long format, one row per (state, window, hour, metric), UTF-8, `.`
decimal separator, header row.

## 6. Errors and edge cases

| Situation | Behaviour |
|---|---|
| Window not planar (deviation > 1e-6 × size), self-intersecting or zero area | Error, nothing computed |
| Sun vector zero or NaN | Error |
| Sun vector not unit | Normalised silently |
| DNI negative or NaN | Error |
| Degenerate (zero-area) triangles | Skipped, counted, warning |
| EPW missing values (9999) | Hours dropped and listed, warning |
| Meshing fails for one state | That state outputs `null`, warning; other states computed |
| Sun behind or parallel to the glass | `f = 0`, `E_dir = 0` (not an error) |
| Shade touching or crossing the glass plane | Handled by the step-2 clip |
| Sun vectors all `z < 0` | Treated as Ladybug convention (towards ground) and reversed |
| Sun vectors all `z > 0` | Used as is |
| Sun vectors mixed sign in `z` | Error, unless the direction override input is set |
| Window normal | Optional input; default from surface normal or right-hand rule; always drawn as an arrow in the preview |

The Core throws `ArgumentException` with an explicit message; the plugin maps errors to
red runtime messages and skipped/counted items to orange warnings.

## 7. Testing and validation

### 7.1 Core tests (CI, Windows + macOS, gate merges)

- **Analytic cases (tolerance 1e-6):** infinite overhang and fin against closed form;
  no shade; full cover; sun parallel to glass; shade entirely behind the glass
  (`f = 1`); concave L-shaped shade; frame with an opening equal to the window
  (`f = 1` at normal incidence).
- **Invariants (random scenes):** sunlit + shaded area = window area; result unchanged
  under translation or rotation of the whole scene together with the sun.
- **Ray-casting oracle:** ~500 random scenes from fixed seeds (random triangles,
  concave meshes, slats, geometry on both sides of the glass, random suns). Brute-force
  reference: 401 × 401 grid on the window (prime size, so that evenly spaced slat edges
  do not all fall at the same offset from the grid rows), one Möller–Trumbore ray per
  point towards the sun against every triangle. Tolerance 0.01 (grid discretisation).
  A failure prints its seed.
- **Solar:** SPA against the NREL reference case (Reda & Andreas 2004: Golden, CO,
  2003-10-17 12:30:30, UTC−7) within 1e-4°; EPW reader on a sample file including
  missing values.

### 7.2 EnergyPlus validation (manual, `validation/`)

- Each case is one scene JSON; the same file generates the EnergyPlus IDF.
- EnergyPlus settings: `ShadowCalculation` with *PixelCounting* and update frequency
  method *Timestep* (the default periodic averaging over 20 days would invalidate an
  hourly comparison). Output hourly `Surface Outside Face Sunlit Fraction`,
  `Site Solar Azimuth Angle` and `Site Solar Altitude Angle`.
- **The engine is fed EnergyPlus's own solar angles**, not SPA, so that only geometry
  is compared.
- **Sunny hour:** sun above the horizon and `s·n > 0`.
- `SunTools.Validate` computes p95 of |Δf| pooled over all sunny hours of all cases and
  writes a metrics JSON (per-case p50/p95/max as well).
- Cases: overhang, fin, concave, multi-slat; then awning, venetian blind and two-axis
  tracker rebuilt with current tools.

### 7.3 Grasshopper integration (manual)

- Example `.gh` files for the three typologies.
- Checklist in Rhino 8 for macOS: plugin loads, normal arrows, preview, CSV export.
- Automated Rhino.Inside tests only if a need appears.

### 7.4 Performance (indicative, not a gate)

Venetian case, 81 states × ~4,400 sunny hours: under 30 s on a laptop.
