using System;

namespace SunTools.Core.Solar;

/// <summary>
/// NREL Solar Position Algorithm: Reda &amp; Andreas (2004), "Solar position algorithm for solar
/// radiation applications", Solar Energy 76, 577-589. Step numbers in comments refer to section 3
/// of the paper. Valid for years −2000 to 6000; uncertainty ±0.0003°.
/// </summary>
public static class Spa
{
    /// <summary>Default ΔT = TT − UT in seconds (value for the 2020s).</summary>
    public const double DefaultDeltaT = 69.0;

    /// <summary>Atmospheric refraction at sunrise and sunset, degrees (paper default).</summary>
    public const double AtmosphericRefraction = 0.5667;

    private const double SunRadius = 0.26667; // degrees

    /// <summary>
    /// Sun position at a local standard time. <paramref name="timeZone"/> in hours (negative west of
    /// Greenwich), latitude and longitude in degrees (east positive), elevation in metres, pressure in
    /// mbar, temperature in °C, ΔT in seconds.
    /// </summary>
    public static SolarPosition Compute(int year, int month, int day, int hour, int minute, double second,
        double timeZone, double latitude, double longitude, double elevation, double pressure,
        double temperature, double deltaT = DefaultDeltaT)
    {
        var d = ComputeDetailed(year, month, day, hour, minute, second, timeZone, latitude, longitude,
            elevation, pressure, temperature, deltaT);
        return new SolarPosition(d.Zenith, d.Azimuth, d.JulianDay);
    }

    /// <summary>
    /// Incidence angle (degrees) between the sun and the normal of a surface (paper §3.15).
    /// <paramref name="surfaceSlope"/>: tilt from horizontal. <paramref name="surfaceAzimuthRotation"/>:
    /// measured from south to the projection of the surface normal, negative towards east.
    /// </summary>
    public static double Incidence(SolarPosition position, double surfaceSlope, double surfaceAzimuthRotation)
    {
        if (position == null) throw new ArgumentNullException(nameof(position));
        Check(surfaceSlope, -360, 360, nameof(surfaceSlope));
        Check(surfaceAzimuthRotation, -360, 360, nameof(surfaceAzimuthRotation));
        double zenith = Rad(position.Zenith);
        double slope = Rad(surfaceSlope);
        double gamma = Rad(position.Azimuth - 180); // astronomers' azimuth, from south
        double cos = Math.Cos(zenith) * Math.Cos(slope)
                     + Math.Sin(slope) * Math.Sin(zenith) * Math.Cos(gamma - Rad(surfaceAzimuthRotation));
        return Deg(Math.Acos(Math.Max(-1, Math.Min(1, cos))));
    }

    internal static SpaDetail ComputeDetailed(int year, int month, int day, int hour, int minute, double second,
        double timeZone, double latitude, double longitude, double elevation, double pressure,
        double temperature, double deltaT = DefaultDeltaT)
    {
        Check(year, -2000, 6000, nameof(year));
        Check(month, 1, 12, nameof(month));
        Check(day, 1, 31, nameof(day));
        Check(hour, 0, 24, nameof(hour));
        Check(minute, 0, 59, nameof(minute));
        if (!(second >= 0 && second < 60))
            throw new ArgumentException($"second must be in [0, 60), got {second}.", nameof(second));
        if (hour == 24 && (minute > 0 || second > 0))
            throw new ArgumentException("hour 24 requires minute = 0 and second = 0.", nameof(hour));
        Check(timeZone, -18, 18, nameof(timeZone));
        Check(latitude, -90, 90, nameof(latitude));
        Check(longitude, -180, 180, nameof(longitude));
        Check(elevation, -6500000, double.MaxValue, nameof(elevation));
        Check(pressure, 0, 5000, nameof(pressure));
        Check(temperature, -273, 6000, nameof(temperature));
        Check(deltaT, -8000, 8000, nameof(deltaT));

        var d = new SpaDetail();

        // 3.1 Julian and ephemeris days, centuries and millennium.
        d.JulianDay = JulianDay(year, month, day, hour, minute, second, timeZone);
        double jc = (d.JulianDay - 2451545.0) / 36525.0;
        d.JulianEphemerisDay = d.JulianDay + deltaT / 86400.0;
        double jce = (d.JulianEphemerisDay - 2451545.0) / 36525.0;
        double jme = jce / 10.0;

        // 3.2 Earth heliocentric longitude, latitude, radius vector.
        d.L = Limit360(Deg(Series(SpaTables.L, jme)));
        d.B = Deg(Series(SpaTables.B, jme));
        d.R = Series(SpaTables.R, jme);

        // 3.3 Geocentric longitude and latitude.
        double theta = Limit360(d.L + 180.0);
        double beta = -d.B;

        // 3.4 Nutation in longitude and obliquity.
        Nutation(jce, out d.DeltaPsi, out d.DeltaEpsilon);

        // 3.5 True obliquity of the ecliptic.
        double u = jme / 10.0;
        double epsilon0 = 84381.448 + u * (-4680.93 + u * (-1.55 + u * (1999.25 + u * (-51.38 + u * (-249.67
                          + u * (-39.05 + u * (7.12 + u * (27.87 + u * (5.79 + u * 2.45)))))))));
        d.Epsilon = epsilon0 / 3600.0 + d.DeltaEpsilon;

        // 3.6-3.7 Aberration correction and apparent sun longitude.
        double deltaTau = -20.4898 / (3600.0 * d.R);
        d.Lambda = theta + d.DeltaPsi + deltaTau;

        // 3.8 Apparent sidereal time at Greenwich.
        double nu0 = Limit360(280.46061837 + 360.98564736629 * (d.JulianDay - 2451545.0)
                              + jc * jc * (0.000387933 - jc / 38710000.0));
        d.Nu = nu0 + d.DeltaPsi * Math.Cos(Rad(d.Epsilon));

        // 3.9-3.10 Geocentric right ascension and declination.
        double lambdaR = Rad(d.Lambda), epsilonR = Rad(d.Epsilon), betaR = Rad(beta);
        d.Alpha = Limit360(Deg(Math.Atan2(
            Math.Sin(lambdaR) * Math.Cos(epsilonR) - Math.Tan(betaR) * Math.Sin(epsilonR), Math.Cos(lambdaR))));
        d.Delta = Deg(Math.Asin(
            Math.Sin(betaR) * Math.Cos(epsilonR) + Math.Cos(betaR) * Math.Sin(epsilonR) * Math.Sin(lambdaR)));

        // 3.11 Observer local hour angle.
        d.H = Limit360(d.Nu + longitude - d.Alpha);

        // 3.12 Topocentric right ascension and declination (parallax).
        double xi = Rad(8.794 / (3600.0 * d.R));
        double phi = Rad(latitude);
        double uu = Math.Atan(0.99664719 * Math.Tan(phi));
        double x = Math.Cos(uu) + elevation / 6378140.0 * Math.Cos(phi);
        double y = 0.99664719 * Math.Sin(uu) + elevation / 6378140.0 * Math.Sin(phi);
        double hR = Rad(d.H), deltaR = Rad(d.Delta);
        double denominator = Math.Cos(deltaR) - x * Math.Sin(xi) * Math.Cos(hR);
        double deltaAlpha = Math.Atan2(-x * Math.Sin(xi) * Math.Sin(hR), denominator);
        double deltaPrime = Math.Atan2((Math.Sin(deltaR) - y * Math.Sin(xi)) * Math.Cos(deltaAlpha), denominator);

        // 3.13 Topocentric local hour angle.
        double hPrime = hR - deltaAlpha;

        // 3.14 Topocentric zenith angle, with refraction.
        double e0 = Deg(Math.Asin(Math.Sin(phi) * Math.Sin(deltaPrime)
                                  + Math.Cos(phi) * Math.Cos(deltaPrime) * Math.Cos(hPrime)));
        double deltaE = 0;
        if (e0 >= -(SunRadius + AtmosphericRefraction))
            deltaE = pressure / 1010.0 * 283.0 / (273.0 + temperature)
                     * 1.02 / (60.0 * Math.Tan(Rad(e0 + 10.3 / (e0 + 5.11))));
        d.Zenith = 90.0 - (e0 + deltaE);

        // 3.15 Topocentric azimuth (astronomers' Γ from south, then navigators' Φ from north).
        double gamma = Limit360(Deg(Math.Atan2(Math.Sin(hPrime),
            Math.Cos(hPrime) * Math.Sin(phi) - Math.Tan(deltaPrime) * Math.Cos(phi))));
        d.Azimuth = Limit360(gamma + 180.0);
        return d;
    }

    /// <summary>Julian day (UT) of a local standard time; Julian calendar before 1582-10-15.</summary>
    internal static double JulianDay(int year, int month, int day, int hour, int minute, double second,
        double timeZone)
    {
        double dayDecimal = day + (hour - timeZone + (minute + second / 60.0) / 60.0) / 24.0;
        if (month < 3)
        {
            month += 12;
            year--;
        }
        double jd = Math.Floor(365.25 * (year + 4716.0)) + Math.Floor(30.6001 * (month + 1)) + dayDecimal - 1524.5;
        if (jd > 2299160.0)
        {
            double a = Math.Floor(year / 100.0);
            jd += 2 - a + Math.Floor(a / 4);
        }
        return jd;
    }

    private static double Series(double[][,] tables, double jme)
    {
        double sum = 0, power = 1;
        foreach (var table in tables)
        {
            double term = 0;
            for (int i = 0; i < table.GetLength(0); i++)
                term += table[i, 0] * Math.Cos(table[i, 1] + table[i, 2] * jme);
            sum += term * power;
            power *= jme;
        }
        return sum / 1e8;
    }

    private static void Nutation(double jce, out double deltaPsi, out double deltaEpsilon)
    {
        double[] x =
        {
            297.85036 + jce * (445267.111480 + jce * (-0.0019142 + jce / 189474.0)),
            357.52772 + jce * (35999.050340 + jce * (-0.0001603 - jce / 300000.0)),
            134.96298 + jce * (477198.867398 + jce * (0.0086972 + jce / 56250.0)),
            93.27191 + jce * (483202.017538 + jce * (-0.0036825 + jce / 327270.0)),
            125.04452 + jce * (-1934.136261 + jce * (0.0020708 + jce / 450000.0)),
        };
        double psi = 0, eps = 0;
        for (int i = 0; i < SpaTables.NutationY.GetLength(0); i++)
        {
            double argument = 0;
            for (int j = 0; j < 5; j++) argument += x[j] * SpaTables.NutationY[i, j];
            argument = Rad(argument);
            psi += (SpaTables.NutationAbcd[i, 0] + SpaTables.NutationAbcd[i, 1] * jce) * Math.Sin(argument);
            eps += (SpaTables.NutationAbcd[i, 2] + SpaTables.NutationAbcd[i, 3] * jce) * Math.Cos(argument);
        }
        deltaPsi = psi / 36000000.0;
        deltaEpsilon = eps / 36000000.0;
    }

    private static void Check(double value, double min, double max, string name)
    {
        if (!(value >= min && value <= max))
            throw new ArgumentException($"{name} must be in [{min}, {max}], got {value}.", name);
    }

    private static double Limit360(double degrees) => degrees - 360.0 * Math.Floor(degrees / 360.0);
    private static double Rad(double degrees) => degrees * Math.PI / 180.0;
    private static double Deg(double radians) => radians * 180.0 / Math.PI;
}

/// <summary>Intermediate SPA quantities (degrees, AU), exposed for tests.</summary>
internal sealed class SpaDetail
{
    public double JulianDay, JulianEphemerisDay, L, B, R, DeltaPsi, DeltaEpsilon, Epsilon, Lambda, Nu,
        Alpha, Delta, H, Zenith, Azimuth;
}
