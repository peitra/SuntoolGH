using System;
using System.IO;
using System.Linq;
using SunTools.Core.Geometry;
using SunTools.Core.Solar;
using SunTools.Core.Sunlit;
using Xunit;

namespace SunTools.Core.Tests.Solar;

public class SunPathTests
{
    private static readonly Vec3 North = new Vec3(0, 1, 0);
    private static readonly EpwLocation Svalbard = new EpwLocation("Longyearbyen", 78.2, 15.6, 1, 10);
    private static readonly EpwLocation Sydney = new EpwLocation("Sydney", -33.87, 151.21, 10, 39);

    private static EpwFile Epw(EpwLocation loc, int? rowYear = null, Func<int, int, int, double>? dni = null) =>
        EpwFile.Parse(new StringReader(EpwBuilder.Build(loc, rowYear: rowYear, dni: dni)));

    // Non-leap hour of year of (month, day, EPW hour 1-24).
    private static int Hoy(int month, int day, int hour) =>
        (new DateTime(2003, month, day).DayOfYear - 1) * 24 + hour - 1;

    private static int IndexOf(SunPath path, int hoy) => path.Hoy.ToList().IndexOf(hoy);

    private static void Close(Vec3 expected, Vec3 actual, double tolerance)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Z - actual.Z), 0, tolerance);
    }

    [Theory]
    [InlineData(0, 90, 0, 1, 0)]
    [InlineData(90, 90, 1, 0, 0)]
    [InlineData(180, 90, 0, -1, 0)]
    [InlineData(270, 90, -1, 0, 0)]
    [InlineData(123, 0, 0, 0, 1)]
    public void FromAngles_cardinal_directions(double azimuth, double zenith, double x, double y, double z) =>
        Close(new Vec3(x, y, z), SunVectors.FromAngles(azimuth, zenith, North), 1e-12);

    [Fact]
    public void Rotated_north_turns_the_sun_with_it()
    {
        // North along +X: east is -Y.
        Close(new Vec3(0, -1, 0), SunVectors.FromAngles(90, 90, new Vec3(1, 0, 0)), 1e-12);
        Close(new Vec3(1, 0, 0), SunVectors.FromAngles(0, 90, new Vec3(5, 0, 0)), 1e-12);
    }

    [Theory]
    [InlineData(0, 1, 0.1)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 1)]
    public void Non_horizontal_or_zero_north_throws(double x, double y, double z)
    {
        var ex = Assert.Throws<ArgumentException>(() => SunVectors.FromAngles(180, 45, new Vec3(x, y, z)));
        Assert.Contains("north", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Uses_mid_hour_local_time()
    {
        var path = SunPath.FromEpw(Epw(EpwBuilder.Golden), North);
        int k = IndexOf(path, Hoy(10, 17, 13));
        Assert.True(k >= 0);
        var expected = Spa.Compute(2003, 10, 17, 12, 30, 0, -7, 39.742476, -105.1786, 1829, 1013.25, 20, 69);
        Assert.Equal(expected.Zenith, path.Zenith[k], 9);
        Assert.Equal(expected.Azimuth, path.Azimuth[k], 9);
        Close(SunVectors.FromAngles(expected.Azimuth, expected.Zenith, North), path.Sun[k], 1e-12);
        Assert.Equal(500, path.Dni[k]);
    }

    [Fact]
    public void Hour_24_is_23_30_same_day()
    {
        var path = SunPath.FromEpw(Epw(Svalbard), North);
        int k = IndexOf(path, Hoy(6, 21, 24));
        Assert.True(k >= 0, "midnight sun at hour 24 missing");
        var expected = Spa.Compute(2003, 6, 21, 23, 30, 0, 1, 78.2, 15.6, 10, 1013.25, 20, 69);
        Assert.Equal(expected.Zenith, path.Zenith[k], 9);
    }

    [Fact]
    public void Polar_night_has_no_sunny_hours_and_midnight_sun_exists()
    {
        var path = SunPath.FromEpw(Epw(Svalbard), North);
        Assert.DoesNotContain(path.Hoy, h => h >= Hoy(12, 1, 1));
        Assert.Contains(Hoy(6, 21, 1), path.Hoy);
        Assert.Contains(Hoy(6, 21, 24), path.Hoy);
    }

    [Fact]
    public void Southern_hemisphere_noon_sun_is_to_the_north()
    {
        var path = SunPath.FromEpw(Epw(Sydney), North);
        int k = IndexOf(path, Hoy(6, 21, 13));
        Assert.True(k >= 0);
        Assert.True(path.Sun[k].Y > 0.5, $"sun {path.Sun[k]} should point north");
    }

    [Fact]
    public void Northern_hemisphere_noon_sun_is_to_the_south()
    {
        var path = SunPath.FromEpw(Epw(EpwBuilder.Golden), North);
        int k = IndexOf(path, Hoy(12, 21, 13));
        Assert.True(path.Sun[k].Y < -0.5, $"sun {path.Sun[k]} should point south");
    }

    [Fact]
    public void Missing_dni_hours_are_dropped_and_listed()
    {
        int missing = Hoy(1, 2, 13);
        var path = SunPath.FromEpw(Epw(EpwBuilder.Golden, dni: (m, d, h) => m == 1 && d == 2 && h == 13 ? 9999 : 500),
            North);
        Assert.Equal(new[] { missing }, path.MissingDniHoy);
        Assert.DoesNotContain(missing, path.Hoy);
        Assert.Contains(missing + 1, path.Hoy);
    }

    [Fact]
    public void Year_override_is_used()
    {
        var epw = Epw(EpwBuilder.Golden, rowYear: 2004);
        var path = SunPath.FromEpw(epw, North, year: 2001);
        int k = IndexOf(path, Hoy(10, 17, 13));
        var y2001 = Spa.Compute(2001, 10, 17, 12, 30, 0, -7, 39.742476, -105.1786, 1829, 1013.25, 20, 69);
        var y2004 = Spa.Compute(2004, 10, 17, 12, 30, 0, -7, 39.742476, -105.1786, 1829, 1013.25, 20, 69);
        Assert.Equal(y2001.Zenith, path.Zenith[k], 9);
        Assert.True(Math.Abs(y2001.Zenith - y2004.Zenith) > 1e-3);
        var rowYears = SunPath.FromEpw(epw, North); // kept hours differ, so look the hour up again
        Assert.Equal(y2004.Zenith, rowYears.Zenith[IndexOf(rowYears, Hoy(10, 17, 13))], 9);
    }

    [Fact]
    public void Non_leap_year_override_on_a_leap_file_throws()
    {
        var leapFile = EpwFile.Parse(new StringReader(EpwBuilder.Build(EpwBuilder.Golden, leap: true)));
        var ex = Assert.Throws<ArgumentException>(() => SunPath.FromEpw(leapFile, North, year: 2023));
        Assert.Contains("day", ex.Message);
        SunPath.FromEpw(leapFile, North, year: 2024); // a leap override stays possible
    }

    [Fact]
    public void Below_horizon_hours_are_excluded()
    {
        var path = SunPath.FromEpw(Epw(EpwBuilder.Golden), North);
        Assert.All(path.Zenith, z => Assert.True(z < 90));
        Assert.InRange(path.Count, 4000, 4700);
        Assert.Equal(path.Count, path.Sun.Count);
        Assert.Equal(path.Count, path.Dni.Count);
        Assert.Equal(path.Count, path.Azimuth.Count);
        Assert.Empty(path.MissingDniHoy);
    }

    /// <summary>
    /// SPA's own surface-incidence formula (§3.15) against our vector conversion and the engine's
    /// s·n: an unshaded vertical window must receive DNI·cos θ every sunny hour. East and west
    /// windows are needed: a south window is blind to an east/west mirror of the sun vector.
    /// </summary>
    [Theory]
    [InlineData(0, 1, 0, 0, -1, 0, 0)]    // north +Y, window faces south (-Y)
    [InlineData(0, 1, 0, 1, 0, 0, -90)]   // north +Y, window faces east (+X)
    [InlineData(0, 1, 0, -1, 0, 0, 90)]   // north +Y, window faces west (-X)
    [InlineData(1, 0, 0, -1, 0, 0, 0)]    // north +X, window faces south (-X)
    [InlineData(1, 0, 0, 0, -1, 0, -90)]  // north +X, window faces east (-Y)
    public void Unshaded_window_irradiance_equals_SPA_incidence(double nx, double ny, double nz,
        double wx, double wy, double wz, double surfaceAzimuthRotation)
    {
        const double Dni = 800;
        var north = new Vec3(nx, ny, nz);
        var normal = new Vec3(wx, wy, wz);
        var epw = Epw(EpwBuilder.Golden, dni: (m, d, h) => Dni);
        var path = SunPath.FromEpw(epw, north);

        // A 1 × 1 m vertical square whose plane is perpendicular to the normal.
        var side = new Vec3(0, 0, 1).Cross(normal);
        var window = Window.Create(new[]
        {
            new Vec3(0, 0, 0), side, side + new Vec3(0, 0, 1), new Vec3(0, 0, 1),
        }, normal);
        var result = SunlitEngine.Compute(window, ShadeMesh.Empty, path.Sun, path.Dni);

        int sunny = 0;
        for (int k = 0; k < path.Count; k++)
        {
            var r = epw.Records[path.Hoy[k]];
            var position = Spa.Compute(r.Year, r.Month, r.Day, r.Hour - 1, 30, 0, -7, 39.742476, -105.1786,
                1829, 1013.25, 20, 69);
            double cos = Math.Cos(Spa.Incidence(position, 90, surfaceAzimuthRotation) * Math.PI / 180);
            Assert.InRange(Math.Abs(result.DirectIrradiance[k] - Dni * Math.Max(0, cos)), 0, 1e-9 * Dni);
            if (cos > 1e-9)
            {
                Assert.Equal(1.0, result.SunlitFraction[k], 9);
                sunny++;
            }
        }
        Assert.InRange(sunny, 1000, path.Count);
    }

    [Fact]
    public void FromLocation_matches_FromEpw_geometry()
    {
        double standardPa = SunPath.StandardPressure(1829) * 100;
        var text = EpwBuilder.Build(EpwBuilder.Golden, rowYear: 2003,
            edit: (m, d, h, f) => { f[6] = "10"; f[9] = standardPa.ToString("R", System.Globalization.CultureInfo.InvariantCulture); });
        var fromEpw = SunPath.FromEpw(EpwFile.Parse(new StringReader(text)), North);
        var fromLocation = SunPath.FromLocation(39.742476, -105.1786, -7, 1829, 2003, North);
        Assert.Equal(fromEpw.Hoy, fromLocation.Hoy);
        for (int k = 0; k < fromEpw.Count; k++)
        {
            Assert.Equal(fromEpw.Zenith[k], fromLocation.Zenith[k], 9);
            Assert.Equal(fromEpw.Azimuth[k], fromLocation.Azimuth[k], 9);
            Close(fromEpw.Sun[k], fromLocation.Sun[k], 1e-12);
        }
        Assert.True(fromEpw.HasDni);
    }

    [Fact]
    public void FromLocation_leap_year_has_8784_candidate_hours()
    {
        var path = SunPath.FromLocation(39.742476, -105.1786, -7, 1829, 2004, North);
        Assert.InRange(path.Hoy.Max(), 8760, 8783);
        var common = SunPath.FromLocation(39.742476, -105.1786, -7, 1829, 2003, North);
        Assert.InRange(common.Hoy.Max(), 8700, 8759);
    }

    [Fact]
    public void FromLocation_has_no_dni()
    {
        var path = SunPath.FromLocation(48.85, 2.35, 1, 35, 2026, North);
        Assert.False(path.HasDni);
        Assert.All(path.Dni, d => Assert.True(double.IsNaN(d)));
        Assert.Empty(path.MissingDniHoy);
        Assert.InRange(path.Count, 4000, 4700);
    }

    [Fact]
    public void FromLocation_rejects_bad_latitude()
    {
        var ex = Assert.Throws<ArgumentException>(() => SunPath.FromLocation(91, 0, 0, 0, 2026, North));
        Assert.Contains("latitude", ex.Message);
    }

    [Fact]
    public void Missing_pressure_and_temperature_fall_back_to_standard_values()
    {
        var text = EpwBuilder.Build(EpwBuilder.Golden, edit: (m, d, h, f) => { f[6] = "99.9"; f[9] = "999999"; });
        var path = SunPath.FromEpw(EpwFile.Parse(new StringReader(text)), North);
        int k = IndexOf(path, Hoy(10, 17, 13));
        double standard = 1013.25 * Math.Pow(1 - 2.25577e-5 * 1829, 5.25588);
        var expected = Spa.Compute(2003, 10, 17, 12, 30, 0, -7, 39.742476, -105.1786, 1829, standard, 10, 69);
        Assert.Equal(expected.Zenith, path.Zenith[k], 9);
    }
}
