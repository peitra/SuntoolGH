using System;
using System.IO;
using System.Linq;
using System.Text;
using SunTools.Core.Solar;
using Xunit;

namespace SunTools.Core.Tests.Solar;

public class EpwFileTests
{
    private static EpwFile Parse(string text) => EpwFile.Parse(new StringReader(text));

    [Fact]
    public void Parses_location_and_8760_records()
    {
        var epw = Parse(EpwBuilder.Build(EpwBuilder.Golden));
        Assert.Equal("Golden", epw.Location.City);
        Assert.Equal(39.742476, epw.Location.Latitude);
        Assert.Equal(-105.1786, epw.Location.Longitude);
        Assert.Equal(-7, epw.Location.TimeZone);
        Assert.Equal(1829, epw.Location.Elevation);
        Assert.Equal(8760, epw.Records.Count);
        Assert.False(epw.IsLeapYear);
        Assert.Equal(0, epw.Records[0].Hoy);
        Assert.Equal(8759, epw.Records[8759].Hoy);
        var r = epw.Records[0];
        Assert.Equal((2003, 1, 1, 1, 60), (r.Year, r.Month, r.Day, r.Hour, r.Minute));
        Assert.Equal(20.0, r.DryBulb);
        Assert.Equal(101325, r.Pressure);
        Assert.Equal(500, r.Dni);
    }

    [Fact]
    public void Leap_file_has_8784_records_and_Feb_29()
    {
        var epw = Parse(EpwBuilder.Build(EpwBuilder.Golden, leap: true));
        Assert.True(epw.IsLeapYear);
        Assert.Equal(8784, epw.Records.Count);
        var feb29 = epw.Records.Single(r => r.Month == 2 && r.Day == 29 && r.Hour == 1);
        Assert.Equal(59 * 24, feb29.Hoy);
        Assert.Equal(8783, epw.Records[8783].Hoy);
    }

    [Fact]
    public void Non_leap_file_with_leap_row_year_keeps_non_leap_hoy()
    {
        var epw = Parse(EpwBuilder.Build(EpwBuilder.Golden, rowYear: 2004));
        Assert.False(epw.IsLeapYear);
        var march1 = epw.Records.Single(r => r.Month == 3 && r.Day == 1 && r.Hour == 1);
        Assert.Equal(59 * 24, march1.Hoy);
        Assert.Equal(2004, march1.Year);
    }

    [Fact]
    public void Missing_values_become_NaN()
    {
        var epw = Parse(EpwBuilder.Build(EpwBuilder.Golden, edit: (m, d, h, f) =>
        {
            if (m == 1 && d == 2 && h == 13) f[14] = "9999";
            if (m == 1 && d == 2 && h == 14) f[9] = "999999";
            if (m == 1 && d == 2 && h == 15) f[6] = "99.9";
        }));
        var r13 = epw.Records[24 + 12];
        var r14 = epw.Records[24 + 13];
        var r15 = epw.Records[24 + 14];
        Assert.True(double.IsNaN(r13.Dni));
        Assert.Equal(101325, r13.Pressure);
        Assert.Equal(20.0, r13.DryBulb);
        Assert.True(double.IsNaN(r14.Pressure));
        Assert.Equal(500, r14.Dni);
        Assert.True(double.IsNaN(r15.DryBulb));
        Assert.Equal(500, r15.Dni);
    }

    [Fact]
    public void Crlf_trailing_blank_line_and_latin1_city_load()
    {
        var loc = new EpwLocation("Zürich", 47.38, 8.57, 1, 556);
        var text = EpwBuilder.Build(loc, newline: "\r\n", trailingBlankLine: true);
        var path = Path.Combine(Path.GetTempPath(), $"suntools-{Guid.NewGuid():N}.epw");
        try
        {
            File.WriteAllText(path, text, Encoding.Latin1);
            var epw = EpwFile.Load(path);
            Assert.Equal(8760, epw.Records.Count);
            Assert.Equal(47.38, epw.Location.Latitude);
            Assert.Equal(8.57, epw.Location.Longitude);
            Assert.StartsWith("Z", epw.Location.City);
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static TheoryData<string, Func<string[], string[]>, string> Malformed => new()
    {
        { "first line not LOCATION", l => Set(l, 0, "SITE,Golden"), "line 1" },
        { "line 8 not DATA PERIODS", l => Set(l, 7, "COMMENTS 3,"), "line 8" },
        { "non-numeric latitude", l => Set(l, 0, "LOCATION,Golden,XX,USA,TMY3,724666,north,-105.1786,-7,1829"), "line 1" },
        { "8761 records", l => l.Concat(new[] { l[l.Length - 1] }).ToArray(), "8761 records" },
        { "row with 10 fields", l => Set(l, 20, string.Join(",", l[20].Split(',').Take(10))), "line 21" },
        { "hour 0", l => Set(l, 20, Field(l[20], 3, "0")), "line 21" },
        { "hour 25", l => Set(l, 20, Field(l[20], 3, "25")), "line 21" },
        { "rows out of order", l => Swap(l, 20, 21), "line 21" },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Malformed_files_throw(string label, Func<string[], string[]> corrupt, string expectedInMessage)
    {
        var lines = EpwBuilder.Build(EpwBuilder.Golden).TrimEnd('\n').Split('\n');
        var text = string.Join("\n", corrupt(lines)) + "\n";
        var ex = Assert.Throws<ArgumentException>(() => Parse(text));
        Assert.True(ex.Message.Contains(expectedInMessage), $"{label}: message was '{ex.Message}'");
    }

    private static string[] Set(string[] lines, int index, string value)
    {
        var copy = (string[])lines.Clone();
        copy[index] = value;
        return copy;
    }

    private static string[] Swap(string[] lines, int i, int j)
    {
        var copy = (string[])lines.Clone();
        (copy[i], copy[j]) = (copy[j], copy[i]);
        return copy;
    }

    private static string Field(string line, int index, string value)
    {
        var f = line.Split(',');
        f[index] = value;
        return string.Join(",", f);
    }
}
