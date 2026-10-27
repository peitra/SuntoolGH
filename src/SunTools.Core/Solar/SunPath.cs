using System;
using System.Collections.Generic;
using SunTools.Core.Geometry;

namespace SunTools.Core.Solar;

/// <summary>
/// Hourly sun vectors for a year, as parallel lists ready for <c>SunlitEngine.Compute</c>. Only
/// hours with the sun above the horizon are kept. Each hour is evaluated at its middle, in local
/// standard time: EPW hour <c>h</c> (the hour ending at <c>h:00</c>) is computed at <c>h−1:30</c>.
/// </summary>
public sealed class SunPath
{
    /// <summary>Dry-bulb temperature (°C) used for refraction when the weather file has none.</summary>
    public const double FallbackTemperature = 10.0;

    private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    private readonly List<int> _hoy = new List<int>();
    private readonly List<Vec3> _sun = new List<Vec3>();
    private readonly List<double> _dni = new List<double>();
    private readonly List<double> _zenith = new List<double>();
    private readonly List<double> _azimuth = new List<double>();
    private readonly List<int> _missingDniHoy = new List<int>();

    private SunPath(bool hasDni)
    {
        HasDni = hasDni;
        Hoy = _hoy;
        Sun = _sun;
        Dni = _dni;
        Zenith = _zenith;
        Azimuth = _azimuth;
        MissingDniHoy = _missingDniHoy;
    }

    /// <summary>Hour of year (0-based) of each kept hour.</summary>
    public IReadOnlyList<int> Hoy { get; }

    /// <summary>Unit vectors towards the sun, world frame, Z up.</summary>
    public IReadOnlyList<Vec3> Sun { get; }

    /// <summary>Direct normal irradiance, W/m²; all NaN when <see cref="HasDni"/> is false.</summary>
    public IReadOnlyList<double> Dni { get; }

    /// <summary>False for a path built from a location alone (<see cref="FromLocation"/>).</summary>
    public bool HasDni { get; }

    /// <summary>Topocentric zenith angle with refraction, degrees.</summary>
    public IReadOnlyList<double> Zenith { get; }

    /// <summary>Azimuth from north, eastward, degrees.</summary>
    public IReadOnlyList<double> Azimuth { get; }

    /// <summary>Hours dropped because the weather file has no DNI for them (EPW 9999).</summary>
    public IReadOnlyList<int> MissingDniHoy { get; }

    public int Count => Hoy.Count;

    /// <summary>
    /// Sun path of an EPW file. <paramref name="north"/>: horizontal model direction of north (e.g.
    /// +Y). <paramref name="year"/>: overrides the year of every row (TMY rows mix years).
    /// </summary>
    public static SunPath FromEpw(EpwFile epw, Vec3 north, double deltaT = Spa.DefaultDeltaT, int? year = null)
    {
        if (epw == null) throw new ArgumentNullException(nameof(epw));
        SunVectors.UnitNorth(north);
        var loc = epw.Location;
        double standardPressure = StandardPressure(loc.Elevation);
        var path = new SunPath(hasDni: true);
        foreach (var r in epw.Records)
        {
            if (double.IsNaN(r.Dni))
            {
                path._missingDniHoy.Add(r.Hoy);
                continue;
            }
            double pressure = double.IsNaN(r.Pressure) ? standardPressure : r.Pressure / 100.0;
            double temperature = double.IsNaN(r.DryBulb) ? FallbackTemperature : r.DryBulb;
            path.AddHour(r.Hoy, year ?? r.Year, r.Month, r.Day, r.Hour, loc.TimeZone, loc.Latitude, loc.Longitude,
                loc.Elevation, pressure, temperature, deltaT, north, r.Dni);
        }
        return path;
    }

    /// <summary>
    /// Sun path of any site for one calendar year, without weather data: every hour of
    /// <paramref name="year"/> at its middle (local standard time), standard-atmosphere pressure,
    /// 10 °C. <see cref="Dni"/> is all NaN, so <c>SunlitEngine.Compute</c> needs DNI from elsewhere.
    /// </summary>
    public static SunPath FromLocation(double latitude, double longitude, double timeZone, double elevation,
        int year, Vec3 north, double deltaT = Spa.DefaultDeltaT)
    {
        SunVectors.UnitNorth(north);
        bool leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
        double pressure = StandardPressure(elevation);
        var path = new SunPath(hasDni: false);
        int hoy = 0;
        for (int month = 1; month <= 12; month++)
        {
            int days = DaysInMonth[month - 1] + (leap && month == 2 ? 1 : 0);
            for (int day = 1; day <= days; day++)
            for (int hour = 1; hour <= 24; hour++, hoy++)
                path.AddHour(hoy, year, month, day, hour, timeZone, latitude, longitude, elevation, pressure,
                    FallbackTemperature, deltaT, north, double.NaN);
        }
        return path;
    }

    // EPW-style hour (1-24, hour ending) evaluated at its middle; kept only above the horizon.
    private void AddHour(int hoy, int year, int month, int day, int hour, double timeZone, double latitude,
        double longitude, double elevation, double pressure, double temperature, double deltaT, Vec3 north,
        double dni)
    {
        var p = Spa.Compute(year, month, day, hour - 1, 30, 0, timeZone, latitude, longitude, elevation, pressure,
            temperature, deltaT);
        if (p.Elevation <= 0) return;
        _hoy.Add(hoy);
        _sun.Add(SunVectors.FromAngles(p.Azimuth, p.Zenith, north));
        _dni.Add(dni);
        _zenith.Add(p.Zenith);
        _azimuth.Add(p.Azimuth);
    }

    /// <summary>Standard-atmosphere pressure (mbar) at an elevation in metres.</summary>
    internal static double StandardPressure(double elevation) =>
        1013.25 * Math.Pow(1 - 2.25577e-5 * elevation, 5.25588);
}
