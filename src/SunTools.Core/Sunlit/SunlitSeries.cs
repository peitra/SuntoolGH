using System.Collections.Generic;

namespace SunTools.Core.Sunlit;

/// <summary>Direct-sun results for one window and one shade state over many hours.</summary>
public sealed class SunlitSeries
{
    internal SunlitSeries(double[] sunlitFraction, double[] directIrradiance, int degenerateTriangleCount)
    {
        SunlitFraction = sunlitFraction;
        DirectIrradiance = directIrradiance;
        DegenerateTriangleCount = degenerateTriangleCount;
    }

    public IReadOnlyList<double> SunlitFraction { get; }
    public IReadOnlyList<double> DirectIrradiance { get; }
    /// <summary>Zero-area shade triangles that were skipped.</summary>
    public int DegenerateTriangleCount { get; }
    public int Count => SunlitFraction.Count;
}
