using System.Globalization;
using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

public sealed class EetFormatTests
{
    [Theory]
    [InlineData("0.1", "0.10")]
    [InlineData("1234567.8", "1234567.80")]
    [InlineData("-0.00", "0.00")]
    [InlineData("188580", "188580.00")]
    [InlineData("-12.5", "-12.50")]
    [InlineData("25.00", "25.00")]
    public void Amount_FormatsTwoDecimals(string input, string expected)
    {
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        Assert.Equal(expected, EetFormat.Amount(value));
    }

    [Fact]
    public void Amount_IgnoresCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");

            Assert.Equal("1234.50", EetFormat.Amount(1234.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Amount_MoreThanTwoDecimals_Throws()
    {
        Assert.Throws<ArgumentException>(() => EetFormat.Amount(12.345m));
    }

    [Fact]
    public void Timestamp_Utc_UsesZ()
    {
        var value = new DateTimeOffset(2026, 7, 1, 9, 2, 18, TimeSpan.Zero);

        Assert.Equal("2026-07-01T09:02:18Z", EetFormat.Timestamp(value));
    }

    [Fact]
    public void Timestamp_Offset_KeepsOffset()
    {
        var value = new DateTimeOffset(2027, 1, 9, 4, 25, 28, TimeSpan.FromHours(1));

        Assert.Equal("2027-01-09T04:25:28+01:00", EetFormat.Timestamp(value));
    }

    [Fact]
    public void Timestamp_DropsFractionalSeconds()
    {
        var value = new DateTimeOffset(2027, 1, 9, 4, 25, 28, 500, TimeSpan.FromHours(1));

        Assert.Equal("2027-01-09T04:25:28+01:00", EetFormat.Timestamp(value));
    }

    [Fact]
    public void ToPragueTime_Winter_PlusOne()
    {
        var value = new DateTimeOffset(2027, 1, 9, 3, 25, 28, TimeSpan.Zero);

        var prague = EetFormat.ToPragueTime(value);

        Assert.Equal(TimeSpan.FromHours(1), prague.Offset);
        Assert.Equal(value.UtcDateTime, prague.UtcDateTime);
    }

    [Fact]
    public void ToPragueTime_Summer_PlusTwo()
    {
        var value = new DateTimeOffset(2027, 6, 9, 3, 25, 28, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromHours(2), EetFormat.ToPragueTime(value).Offset);
    }

    [Fact]
    public void ResolvePragueZone_IanaMissing_FallsBackToWindowsId()
    {
        var requested = new List<string>();
        TimeZoneInfo Find(string id)
        {
            requested.Add(id);
            return id == "Central Europe Standard Time"
                ? TimeZoneInfo.Utc
                : throw new TimeZoneNotFoundException();
        }

        var zone = EetFormat.ResolvePragueZone(Find);

        Assert.Same(TimeZoneInfo.Utc, zone);
        Assert.Equal(["Europe/Prague", "Central Europe Standard Time"], requested);
    }

    [Fact]
    public void ResolvePragueZone_NoTimeZoneData_UsesEuRules()
    {
        var zone = EetFormat.ResolvePragueZone(_ => throw new TimeZoneNotFoundException());

        Assert.Equal(TimeSpan.FromHours(1), TimeZoneInfo.ConvertTime(new DateTimeOffset(2027, 1, 9, 3, 0, 0, TimeSpan.Zero), zone).Offset);
        Assert.Equal(TimeSpan.FromHours(2), TimeZoneInfo.ConvertTime(new DateTimeOffset(2027, 6, 9, 3, 0, 0, TimeSpan.Zero), zone).Offset);
    }

    [Fact]
    public void ResolvePragueZone_NoTimeZoneData_MatchesSystemZoneAcrossTransitions()
    {
        var builtIn = EetFormat.ResolvePragueZone(_ => throw new TimeZoneNotFoundException());
        var system = EetFormat.ResolvePragueZone(TimeZoneInfo.FindSystemTimeZoneById);

        for (var instant = new DateTimeOffset(2026, 1, 1, 0, 30, 0, TimeSpan.Zero);
             instant < new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero);
             instant = instant.AddHours(1))
        {
            Assert.Equal(TimeZoneInfo.ConvertTime(instant, system).Offset, TimeZoneInfo.ConvertTime(instant, builtIn).Offset);
        }
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Bool_LowerCase(bool value, string expected)
    {
        Assert.Equal(expected, EetFormat.Bool(value));
    }

    [Fact]
    public void Uuid_LowerCase36()
    {
        var text = EetFormat.Uuid(Guid.Parse("03965780-6457-4842-BD80-5F9195C0B8C8"));

        Assert.Equal("03965780-6457-4842-bd80-5f9195c0b8c8", text);
    }
}
