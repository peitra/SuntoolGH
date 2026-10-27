using System;
using SunTools.Core.Solar;
using Xunit;

namespace SunTools.Core.Tests.Solar;

/// <summary>
/// NREL SPA against Reda &amp; Andreas (2004), "Solar position algorithm for solar radiation
/// applications", Solar Energy 76, 577-589: the reference case of its appendix A5 and the
/// Julian-day examples it takes from Meeus.
/// </summary>
public class SpaReferenceTests
{
    // 2003-10-17 12:30:30, UTC-7, Golden (Colorado).
    private static SpaDetail ReferenceDetail() =>
        Spa.ComputeDetailed(2003, 10, 17, 12, 30, 30, -7, 39.742476, -105.1786, 1830.14, 820, 11, 67);

    private static SolarPosition ReferencePosition() =>
        Spa.Compute(2003, 10, 17, 12, 30, 30, -7, 39.742476, -105.1786, 1830.14, 820, 11, 67);

    [Fact]
    public void Reference_case_matches_Reda_and_Andreas_2004()
    {
        var d = ReferenceDetail();
        Assert.Equal(2452930.312847, d.JulianDay, 6);
        Assert.Equal(24.0182616917, d.L, 8);
        Assert.Equal(-0.0001011219, d.B, 9);
        Assert.Equal(0.9965422974, d.R, 9);
        Assert.Equal(-0.00399840, d.DeltaPsi, 7);
        Assert.Equal(0.00166657, d.DeltaEpsilon, 7);
        Assert.Equal(23.440465, d.Epsilon, 6);
        Assert.Equal(202.22741, d.Alpha, 5);
        Assert.Equal(-9.31434, d.Delta, 5);
        Assert.Equal(11.105902, d.H, 6);
        Assert.InRange(Math.Abs(d.Zenith - 50.11162), 0, 1e-4);
        Assert.InRange(Math.Abs(d.Azimuth - 194.34024), 0, 1e-4);
    }

    [Fact]
    public void Compute_returns_the_detailed_angles()
    {
        var p = ReferencePosition();
        var d = ReferenceDetail();
        Assert.Equal(d.Zenith, p.Zenith);
        Assert.Equal(d.Azimuth, p.Azimuth);
        Assert.Equal(90 - d.Zenith, p.Elevation, 12);
        Assert.Equal(d.JulianDay, p.JulianDay);
    }

    [Fact]
    public void Reference_case_incidence_on_tilted_surface() =>
        // Slope 30°, surface azimuth rotation -10° (from south, negative towards east).
        Assert.InRange(Math.Abs(Spa.Incidence(ReferencePosition(), 30, -10) - 25.18700), 0, 1e-4);

    [Theory]
    [InlineData(2000, 1, 1, 0.5, 2451545.0)]
    [InlineData(1999, 1, 1, 0.0, 2451179.5)]
    [InlineData(1987, 1, 27, 0.0, 2446822.5)]
    [InlineData(1987, 6, 19, 0.5, 2446966.0)]
    [InlineData(1988, 1, 27, 0.0, 2447187.5)]
    [InlineData(1988, 6, 19, 0.5, 2447332.0)]
    [InlineData(1900, 1, 1, 0.0, 2415020.5)]
    [InlineData(1600, 1, 1, 0.0, 2305447.5)]
    [InlineData(1600, 12, 31, 0.0, 2305812.5)]
    [InlineData(837, 4, 10, 0.3, 2026871.8)]
    [InlineData(-1000, 7, 12, 0.5, 1356001.0)]
    [InlineData(-1000, 2, 29, 0.0, 1355866.5)]
    [InlineData(-1001, 8, 17, 0.9, 1355671.4)]
    [InlineData(-4712, 1, 1, 0.5, 0.0)]
    public void Julian_day_matches_Meeus(int year, int month, int day, double dayFraction, double expected)
    {
        double hours = dayFraction * 24;
        int hour = (int)Math.Floor(hours + 1e-9);
        double minutes = (hours - hour) * 60;
        int minute = (int)Math.Floor(minutes + 1e-9);
        double second = Math.Max(0, (minutes - minute) * 60);
        Assert.Equal(expected, Spa.JulianDay(year, month, day, hour, minute, second, 0), 6);
    }

    [Fact]
    public void Tables_have_the_published_term_counts()
    {
        Assert.Equal(new[] { 64, 34, 20, 7, 3, 1 }, Array.ConvertAll(SpaTables.L, t => t.GetLength(0)));
        Assert.Equal(new[] { 5, 2 }, Array.ConvertAll(SpaTables.B, t => t.GetLength(0)));
        Assert.Equal(new[] { 40, 10, 6, 2, 1 }, Array.ConvertAll(SpaTables.R, t => t.GetLength(0)));
        Assert.Equal(63, SpaTables.NutationY.GetLength(0));
        Assert.Equal(63, SpaTables.NutationAbcd.GetLength(0));
    }

    [Theory]
    [InlineData(2023, 2, 29)]   // not a leap year
    [InlineData(1900, 2, 29)]   // Gregorian century rule
    [InlineData(2023, 4, 31)]
    [InlineData(2023, 11, 31)]
    public void Impossible_calendar_dates_throw(int year, int month, int day)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Spa.Compute(year, month, day, 12, 0, 0, 0, 45, 0, 0, 1013.25, 10));
        Assert.Contains("day", ex.Message);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2000, 2, 29)]
    [InlineData(1500, 2, 29)]   // Julian calendar: every fourth year
    [InlineData(-1000, 2, 29)]
    public void Real_leap_days_are_accepted(int year, int month, int day) =>
        Spa.Compute(year, month, day, 12, 0, 0, 0, 45, 0, 0, 1013.25, 10);

    [Theory]
    [InlineData("year", -2001)]
    [InlineData("year", 6001)]
    [InlineData("month", 0)]
    [InlineData("month", 13)]
    [InlineData("day", 0)]
    [InlineData("day", 32)]
    [InlineData("hour", -1)]
    [InlineData("hour", 25)]
    [InlineData("minute", -1)]
    [InlineData("minute", 60)]
    [InlineData("second", -0.5)]
    [InlineData("second", 60)]
    [InlineData("timeZone", -18.5)]
    [InlineData("timeZone", 18.5)]
    [InlineData("longitude", -180.1)]
    [InlineData("longitude", 180.1)]
    [InlineData("latitude", -90.1)]
    [InlineData("latitude", 90.1)]
    [InlineData("latitude", double.NaN)]
    [InlineData("elevation", -6500001)]
    [InlineData("pressure", -1)]
    [InlineData("pressure", 5001)]
    [InlineData("temperature", -274)]
    [InlineData("temperature", 6001)]
    [InlineData("deltaT", -8001)]
    [InlineData("deltaT", 8001)]
    public void Out_of_range_input_throws(string field, double value)
    {
        int year = 2003, month = 10, day = 17, hour = 12, minute = 30;
        double second = 30, timeZone = -7, latitude = 39.742476, longitude = -105.1786,
            elevation = 1830.14, pressure = 820, temperature = 11, deltaT = 67;
        switch (field)
        {
            case "year": year = (int)value; break;
            case "month": month = (int)value; break;
            case "day": day = (int)value; break;
            case "hour": hour = (int)value; break;
            case "minute": minute = (int)value; break;
            case "second": second = value; break;
            case "timeZone": timeZone = value; break;
            case "longitude": longitude = value; break;
            case "latitude": latitude = value; break;
            case "elevation": elevation = value; break;
            case "pressure": pressure = value; break;
            case "temperature": temperature = value; break;
            case "deltaT": deltaT = value; break;
            default: throw new InvalidOperationException(field);
        }
        var ex = Assert.Throws<ArgumentException>(() => Spa.Compute(year, month, day, hour, minute, second,
            timeZone, latitude, longitude, elevation, pressure, temperature, deltaT));
        Assert.Contains(field, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
