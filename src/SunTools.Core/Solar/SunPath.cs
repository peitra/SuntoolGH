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

    private SunPath(List<int> hoy, List<Vec3> sun, List<double> dni, List<double> zenith, List<double> azimuth,
        List<int> missingDniHoy)
    {
        Hoy = hoy;
        Sun = sun;
        Dni = dni;
        Zenith = zenith;
        Azimuth = azimuth;
        MissingDniHoy = missingDniHoy;
    }

    /// <summary>Hour of year (0-based) of each kept hour.</summary>
    public IReadOnlyList<int> Hoy { get; }

    /// <summary>Unit vectors towards the sun, world frame, Z up.</summary>
    public IReadOnlyList<Vec3> Sun { get; }

    /// <summary>Direct normal irradiance, W/m².</summary>
    public IReadOnlyList<double> Dni { get; }

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

        var hoy = new List<int>();
        var sun = new List<Vec3>();
        var dni = new List<double>();
        var zenith = new List<double>();
        var azimuth = new List<double>();
        var missing = new List<int>();
        foreach (var r in epw.Records)
        {
            if (double.IsNaN(r.Dni))
            {
                missing.Add(r.Hoy);
                continue;
            }
            double pressure = double.IsNaN(r.Pressure) ? standardPressure : r.Pressure / 100.0;
            double temperature = double.IsNaN(r.DryBulb) ? FallbackTemperature : r.DryBulb;
            var p = Spa.Compute(year ?? r.Year, r.Month, r.Day, r.Hour - 1, 30, 0, loc.TimeZone, loc.Latitude,
                loc.Longitude, loc.Elevation, pressure, temperature, deltaT);
            if (p.Elevation <= 0) continue;
            hoy.Add(r.Hoy);
            sun.Add(SunVectors.FromAngles(p.Azimuth, p.Zenith, north));
            dni.Add(r.Dni);
            zenith.Add(p.Zenith);
            azimuth.Add(p.Azimuth);
        }
        return new SunPath(hoy, sun, dni, zenith, azimuth, missing);
    }

    /// <summary>Standard-atmosphere pressure (mbar) at an elevation in metres.</summary>
    internal static double StandardPressure(double elevation) =>
        1013.25 * Math.Pow(1 - 2.25577e-5 * elevation, 5.25588);
}
