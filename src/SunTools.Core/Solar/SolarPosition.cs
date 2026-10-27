namespace SunTools.Core.Solar;

/// <summary>Topocentric sun position returned by <see cref="Spa.Compute"/>. Angles in degrees.</summary>
public sealed class SolarPosition
{
    internal SolarPosition(double zenith, double azimuth, double julianDay)
    {
        Zenith = zenith;
        Azimuth = azimuth;
        JulianDay = julianDay;
    }

    /// <summary>Topocentric zenith angle, corrected for atmospheric refraction.</summary>
    public double Zenith { get; }

    /// <summary>Azimuth measured from north, eastward, in [0, 360).</summary>
    public double Azimuth { get; }

    /// <summary>Apparent elevation above the horizon, <c>90 − Zenith</c>.</summary>
    public double Elevation => 90 - Zenith;

    /// <summary>Julian day (UT) of the instant computed.</summary>
    public double JulianDay { get; }
}
