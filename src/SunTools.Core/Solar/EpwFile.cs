using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SunTools.Core.Solar;

/// <summary>Site of an EPW file (LOCATION header line).</summary>
public sealed class EpwLocation
{
    internal EpwLocation(string city, double latitude, double longitude, double timeZone, double elevation)
    {
        City = city;
        Latitude = latitude;
        Longitude = longitude;
        TimeZone = timeZone;
        Elevation = elevation;
    }

    public string City { get; }

    /// <summary>Degrees, north positive.</summary>
    public double Latitude { get; }

    /// <summary>Degrees, east positive.</summary>
    public double Longitude { get; }

    /// <summary>Hours from UTC of the file's local standard time.</summary>
    public double TimeZone { get; }

    /// <summary>Metres above sea level.</summary>
    public double Elevation { get; }
}

/// <summary>One hourly row of an EPW file. Missing values are <see cref="double.NaN"/>.</summary>
public sealed class EpwRecord
{
    internal EpwRecord(int year, int month, int day, int hour, int minute, int hoy, double dryBulb,
        double pressure, double dni)
    {
        Year = year;
        Month = month;
        Day = day;
        Hour = hour;
        Minute = minute;
        Hoy = hoy;
        DryBulb = dryBulb;
        Pressure = pressure;
        Dni = dni;
    }

    public int Year { get; }
    public int Month { get; }
    public int Day { get; }

    /// <summary>1-24: the hour ending at <c>Hour:00</c>, local standard time.</summary>
    public int Hour { get; }

    public int Minute { get; }

    /// <summary>Hour of year, 0-based: <c>(dayOfYear − 1)·24 + Hour − 1</c>.</summary>
    public int Hoy { get; }

    /// <summary>Dry-bulb temperature, °C.</summary>
    public double DryBulb { get; }

    /// <summary>Station pressure, Pa.</summary>
    public double Pressure { get; }

    /// <summary>Direct normal irradiance, Wh/m² over the hour (= mean W/m²).</summary>
    public double Dni { get; }
}

/// <summary>
/// EnergyPlus weather file (EPW): 8 header lines, then one row per hour (8,760, or 8,784 for a leap
/// year). Field layout from the EnergyPlus "Auxiliary Programs" weather data dictionary.
/// </summary>
public sealed class EpwFile
{
    private const int HeaderLines = 8;
    private const int MinFields = 15;
    private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    private EpwFile(EpwLocation location, IReadOnlyList<EpwRecord> records, bool isLeapYear)
    {
        Location = location;
        Records = records;
        IsLeapYear = isLeapYear;
    }

    public EpwLocation Location { get; }
    public IReadOnlyList<EpwRecord> Records { get; }

    /// <summary>True for an 8,784-row file; HOY then follows the leap calendar.</summary>
    public bool IsLeapYear { get; }

    public static EpwFile Load(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Parse(reader);
    }

    public static EpwFile Parse(TextReader reader)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));

        var header = new string[HeaderLines];
        for (int i = 0; i < HeaderLines; i++)
            header[i] = reader.ReadLine() ?? throw Error(i + 1, "file ends inside the 8 header lines");
        if (!header[0].StartsWith("LOCATION", StringComparison.OrdinalIgnoreCase))
            throw Error(1, "expected the LOCATION header line");
        if (!header[HeaderLines - 1].StartsWith("DATA PERIODS", StringComparison.OrdinalIgnoreCase))
            throw Error(HeaderLines, "expected the DATA PERIODS header line");
        var location = ParseLocation(header[0]);

        var rows = new List<(int Line, string[] Fields)>(8784);
        int lineNumber = HeaderLines;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (line.Trim().Length == 0) continue;
            rows.Add((lineNumber, line.Split(',')));
        }
        if (rows.Count != 8760 && rows.Count != 8784)
            throw new ArgumentException(
                $"EPW must hold 8760 or 8784 hourly records, found {rows.Count} records " +
                "(sub-hourly or truncated EPW not supported).");
        bool leap = rows.Count == 8784;

        var records = new EpwRecord[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var (n, f) = rows[i];
            if (f.Length < MinFields)
                throw Error(n, $"expected at least {MinFields} fields, found {f.Length}");
            int year = Int(f[0], n, "year");
            int month = Int(f[1], n, "month");
            int day = Int(f[2], n, "day");
            int hour = Int(f[3], n, "hour");
            int minute = Int(f[4], n, "minute");
            if (month < 1 || month > 12) throw Error(n, $"month {month} out of range");
            int days = DaysInMonth[month - 1] + (leap && month == 2 ? 1 : 0);
            if (day < 1 || day > days) throw Error(n, $"day {day} out of range for month {month}");
            if (hour < 1 || hour > 24) throw Error(n, $"hour {hour} out of range 1-24");
            int hoy = (DayOfYear(month, day, leap) - 1) * 24 + hour - 1;
            if (hoy != i)
                throw Error(n, $"expected hour-of-year {i}, found {hoy} (records must be hourly and in calendar order)");
            records[i] = new EpwRecord(year, month, day, hour, minute, hoy,
                Value(f[6], n, "dry bulb", 99.9),
                Value(f[9], n, "pressure", 999999),
                Value(f[14], n, "DNI", 9999));
        }
        return new EpwFile(location, records, leap);
    }

    private static EpwLocation ParseLocation(string line)
    {
        var f = line.Split(',');
        if (f.Length < 10) throw Error(1, $"LOCATION needs 10 fields, found {f.Length}");
        return new EpwLocation(f[1].Trim(),
            Number(f[6], 1, "latitude"), Number(f[7], 1, "longitude"),
            Number(f[8], 1, "time zone"), Number(f[9], 1, "elevation"));
    }

    private static int DayOfYear(int month, int day, bool leap)
    {
        int doy = day;
        for (int m = 1; m < month; m++) doy += DaysInMonth[m - 1] + (leap && m == 2 ? 1 : 0);
        return doy;
    }

    private static double Number(string s, int line, string what)
    {
        if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ||
            double.IsNaN(v) || double.IsInfinity(v))
            throw Error(line, $"{what} '{s}' is not a number");
        return v;
    }

    private static int Int(string s, int line, string what)
    {
        double v = Number(s, line, what);
        if (v != Math.Floor(v)) throw Error(line, $"{what} '{s}' is not an integer");
        return (int)v;
    }

    // EPW marks a missing value with a sentinel (e.g. 9999 for DNI); anything at or above it is missing.
    private static double Value(string s, int line, string what, double missing)
    {
        double v = Number(s, line, what);
        return v >= missing ? double.NaN : v;
    }

    private static ArgumentException Error(int line, string message) =>
        new ArgumentException($"EPW line {line}: {message}.");
}
