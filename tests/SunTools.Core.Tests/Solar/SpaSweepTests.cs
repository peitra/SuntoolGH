using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SunTools.Core.Solar;
using Xunit;

namespace SunTools.Core.Tests.Solar;

/// <summary>
/// SPA against 1,000 points from pvlib's independent implementation of the same algorithm
/// (Reference/make_spa_reference.py). Same equations, separate code: agreement to 1e-6° rules out
/// transcription errors that the single published reference case cannot see.
/// </summary>
public class SpaSweepTests
{
    private const double Tolerance = 1e-6; // degrees

    private static readonly string ReferencePath =
        Path.Combine(AppContext.BaseDirectory, "Solar", "Reference", "spa_reference.csv");

    private static List<double[]> Rows() =>
        File.ReadLines(ReferencePath).Skip(1).Where(l => l.Length > 0)
            .Select(l => l.Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray())
            .ToList();

    [Fact]
    public void Reference_file_has_1000_rows() => Assert.Equal(1000, Rows().Count);

    [Fact]
    public void Every_reference_point_matches_pvlib()
    {
        var failures = new List<string>();
        var rows = Rows();
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var p = Spa.Compute((int)r[0], (int)r[1], (int)r[2], (int)r[3], (int)r[4], r[5], r[6], r[7], r[8],
                r[9], r[10], r[11], r[12]);
            double dZenith = Math.Abs(p.Zenith - r[13]);
            double dAzimuth = Math.Abs(p.Azimuth - r[14]);
            dAzimuth = Math.Min(dAzimuth, 360 - dAzimuth);
            if (r[13] < 1e-3) dAzimuth = 0; // azimuth undefined at the zenith
            if (dZenith > Tolerance || dAzimuth > Tolerance)
                failures.Add($"row {i + 1}: dZenith {dZenith:E2} dAzimuth {dAzimuth:E2} inputs " +
                             string.Join(",", r.Take(13).Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
        }
        Assert.True(failures.Count == 0, $"{failures.Count} points off:\n" + string.Join("\n", failures.Take(20)));
    }
}
