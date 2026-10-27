using System;
using System.Globalization;
using System.Text;
using SunTools.Core.Solar;

namespace SunTools.Core.Tests.Solar;

/// <summary>Synthesises EPW text: the 8 standard header lines and 35-field hourly rows.</summary>
internal static class EpwBuilder
{
    public static readonly EpwLocation Golden = new EpwLocation("Golden", 39.742476, -105.1786, -7, 1829);

    private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    /// <param name="dni">(month, day, hour) → DNI in Wh/m²; default 500.</param>
    /// <param name="edit">(month, day, hour, fields) → mutate any field of a row.</param>
    public static string Build(EpwLocation loc, bool leap = false, int? rowYear = null,
        Func<int, int, int, double>? dni = null, string newline = "\n", bool trailingBlankLine = false,
        Action<int, int, int, string[]>? edit = null)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append(newline);
        Line(string.Format(c, "LOCATION,{0},XX,USA,TMY3,724666,{1},{2},{3},{4}",
            loc.City, loc.Latitude, loc.Longitude, loc.TimeZone, loc.Elevation));
        Line("DESIGN CONDITIONS,0");
        Line("TYPICAL/EXTREME PERIODS,0");
        Line("GROUND TEMPERATURES,0");
        Line("HOLIDAYS/DAYLIGHT SAVINGS,No,0,0,0");
        Line("COMMENTS 1,synthetic test file");
        Line("COMMENTS 2,");
        Line("DATA PERIODS,1,1,Data,Sunday, 1/ 1,12/31");
        int year = rowYear ?? (leap ? 2004 : 2003);
        for (int month = 1; month <= 12; month++)
        {
            int days = DaysInMonth[month - 1] + (leap && month == 2 ? 1 : 0);
            for (int day = 1; day <= days; day++)
            for (int hour = 1; hour <= 24; hour++)
            {
                var f = new string[35];
                for (int i = 0; i < f.Length; i++) f[i] = "0";
                f[0] = year.ToString(c);
                f[1] = month.ToString(c);
                f[2] = day.ToString(c);
                f[3] = hour.ToString(c);
                f[4] = "60";
                f[5] = "?9?9?9?9E0?9?9?9?9?9?9?9?9?9?9?9?9?9?9?9*9*9?9*9*9";
                f[6] = "20.0";
                f[9] = "101325";
                f[14] = (dni?.Invoke(month, day, hour) ?? 500).ToString(c);
                edit?.Invoke(month, day, hour, f);
                Line(string.Join(",", f));
            }
        }
        if (trailingBlankLine) sb.Append(newline);
        return sb.ToString();
    }
}
