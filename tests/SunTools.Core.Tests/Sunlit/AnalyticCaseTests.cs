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
