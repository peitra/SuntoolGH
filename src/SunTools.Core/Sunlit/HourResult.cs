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
