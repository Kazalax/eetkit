using System.Globalization;

namespace EetKit.Internal;

/// <summary>Text forms of values exactly as the EET message requires them.</summary>
internal static class EetFormat
{
    private const int AmountDecimals = 2;
    private const string IanaPragueZoneId = "Europe/Prague";
    private const string WindowsPragueZoneId = "Central Europe Standard Time";
    private const string UtcTimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string OffsetTimestampFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    private static readonly Lazy<TimeZoneInfo> PragueZone =
        new(() => ResolvePragueZone(TimeZoneInfo.FindSystemTimeZoneById));

    /// <summary>Exactly two decimals, invariant culture, negative zero as 0.00.</summary>
    /// <exception cref="ArgumentException">The value has more than two decimals.</exception>
    public static string Amount(decimal value)
    {
        if (decimal.Round(value, AmountDecimals) != value)
        {
            throw new ArgumentException("The amount has more than two decimals.", nameof(value));
        }

        // decimal keeps the sign of zero, so -0.00 has to be normalized explicitly.
        var normalized = value == 0m ? 0m : value;
        return normalized.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Time with the offset it carries, Z for UTC, without fractional seconds.</summary>
    public static string Timestamp(DateTimeOffset value)
    {
        var wholeSeconds = value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
        var format = wholeSeconds.Offset == TimeSpan.Zero ? UtcTimestampFormat : OffsetTimestampFormat;
        return wholeSeconds.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>The same instant in Czech local time (+01:00 in winter, +02:00 in summer).</summary>
    public static DateTimeOffset ToPragueTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, PragueZone.Value);

    /// <summary>
    /// Finds the Prague zone by its IANA id, then by the Windows id that .NET Framework knows,
    /// and finally builds it from the EU rules when the system has no time zone data at all
    /// (for example a container image without tzdata).
    /// </summary>
    public static TimeZoneInfo ResolvePragueZone(Func<string, TimeZoneInfo> findById)
    {
        foreach (var id in new[] { IanaPragueZoneId, WindowsPragueZoneId })
        {
            try
            {
                return findById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Not known on this system, try the next id.
            }
            catch (InvalidTimeZoneException)
            {
                // Corrupted system data, try the next id.
            }
        }

        return CreateEuCentralZone();
    }

    // CET/CEST: UTC+1, summer time from the last Sunday of March 02:00 local standard time
    // to the last Sunday of October 03:00 local summer time (both 01:00 UTC).
    private static TimeZoneInfo CreateEuCentralZone()
    {
        const int LastWeek = 5;
        var summerStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, LastWeek, DayOfWeek.Sunday);
        var summerEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, LastWeek, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), summerStart, summerEnd);
        return TimeZoneInfo.CreateCustomTimeZone(IanaPragueZoneId, TimeSpan.FromHours(1), "Europe/Prague", "CET", "CEST", [rule]);
    }

    public static string Bool(bool value) => value ? "true" : "false";

    public static string Uuid(Guid value) => value.ToString("D");
}
