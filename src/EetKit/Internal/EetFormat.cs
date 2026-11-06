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
    /// Finds the Prague zone by its IANA id, falling back to the Windows id that
    /// .NET Framework on Windows knows instead.
    /// </summary>
    public static TimeZoneInfo ResolvePragueZone(Func<string, TimeZoneInfo> findById)
    {
        try
        {
            return findById(IanaPragueZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return findById(WindowsPragueZoneId);
        }
    }

    public static string Bool(bool value) => value ? "true" : "false";

    public static string Uuid(Guid value) => value.ToString("D");
}
