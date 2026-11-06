using System.Text.RegularExpressions;

namespace EetKit.Internal;

internal sealed record TrzbaValidationResult(
    IReadOnlyList<EetValidationError> Errors,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Checks sale data before sending. Errors follow the XSD (the server would reject the message),
/// warnings follow the server's pass-through checks (popis rozhraní 2.2.4: POK is still issued).
/// </summary>
internal static class TrzbaValidator
{
    private const int MinIdJednotky = 1;
    private const int MaxIdJednotky = 999_999_999;
    private const int MinIdJednotkyWithTwoDigits = 10;
    private const int MaxAmountDecimals = 2;
    private const decimal AmountLimit = 100_000_000m;
    private const int MaxSaleAgeYears = 2;

    private static readonly TimeSpan MaxSaleAheadOfNow = TimeSpan.FromHours(2);
    private static readonly TimeSpan PragueWinterOffset = TimeSpan.FromHours(1);
    private static readonly TimeSpan PragueSummerOffset = TimeSpan.FromHours(2);

    // Minimum sale dates per environment, Czech local midnight (popis rozhraní 2.2.4, bod 4).
    private static readonly DateTimeOffset PlaygroundMinimum = new(2026, 7, 1, 0, 0, 0, PragueSummerOffset);
    private static readonly DateTimeOffset ProductionTransitionMinimum = new(2026, 11, 1, 0, 0, 0, PragueWinterOffset);
    private static readonly DateTimeOffset ProductionStart = new(2027, 1, 1, 0, 0, 0, PragueWinterOffset);

    // Patterns copied from EETXMLSchema.xsd (CZEICType, string20, string25), anchored with \A and \z.
    private static readonly Regex EicPattern = new(@"\ACZ[0-9]{8,10}\z", RegexOptions.CultureInvariant);
    private static readonly Regex IdPoklPattern = new(@"\A[0-9a-zA-Z\.,:;/#\-_ ]{1,20}\z", RegexOptions.CultureInvariant);
    private static readonly Regex PoradCisPattern = new(@"\A[0-9a-zA-Z\.,:;/#\-_ ]{1,25}\z", RegexOptions.CultureInvariant);

    public static TrzbaValidationResult Validate(Trzba trzba, EetEnvironment environment, DateTimeOffset now)
    {
        Guard.NotNull(trzba, nameof(trzba));

        var errors = new List<EetValidationError>();
        var warnings = new List<string>();

        CheckPattern(errors, "eic_popl", trzba.EicPopl, EicPattern, "musí mít tvar CZ a 8 až 10 číslic");
        if (trzba.EicPoverujiciho is not null)
        {
            CheckPattern(errors, "eic_poverujiciho", trzba.EicPoverujiciho, EicPattern, "musí mít tvar CZ a 8 až 10 číslic");
        }

        if (trzba.IdJednotky < MinIdJednotky || trzba.IdJednotky > MaxIdJednotky)
        {
            errors.Add(new EetValidationError("id_jednotky", "musí být celé číslo od 1 do 999999999"));
        }

        CheckPattern(errors, "id_pokl", trzba.IdPokl, IdPoklPattern, "smí mít 1 až 20 znaků 0-9 a-z A-Z . , : ; / # - _ a mezeru");
        CheckPattern(errors, "porad_cis", trzba.PoradCis, PoradCisPattern, "smí mít 1 až 25 znaků 0-9 a-z A-Z . , : ; / # - _ a mezeru");

        CheckAmount(errors, "celk_trzba", trzba.CelkTrzba);
        CheckAmount(errors, "urceno_cerp_zuct", trzba.UrcenoCerpZuct);
        CheckAmount(errors, "cerp_zuct", trzba.CerpZuct);

        AddWarnings(warnings, trzba, environment, now);

        return new TrzbaValidationResult(errors, warnings);
    }

    private static void CheckPattern(List<EetValidationError> errors, string field, string? value, Regex pattern, string rule)
    {
        if (value is null)
        {
            errors.Add(new EetValidationError(field, "chybí"));
        }
        else if (!pattern.IsMatch(value))
        {
            errors.Add(new EetValidationError(field, rule));
        }
    }

    private static void CheckAmount(List<EetValidationError> errors, string field, decimal? value)
    {
        if (value is not { } amount)
        {
            return;
        }

        if (Math.Abs(amount) >= AmountLimit)
        {
            errors.Add(new EetValidationError(field, "musí být v absolutní hodnotě menší než 100 000 000"));
        }
        else if (decimal.Round(amount, MaxAmountDecimals) != amount)
        {
            errors.Add(new EetValidationError(field, "smí mít nejvýš 2 desetinná místa"));
        }
    }

    private static void AddWarnings(List<string> warnings, Trzba trzba, EetEnvironment environment, DateTimeOffset now)
    {
        if (!HasRegisteredUnitShape(trzba.IdJednotky))
        {
            warnings.Add("id_jednotky nemá tvar přidělovaný v DIS+ (aspoň dvě číslice, poslední 1 až 4).");
        }

        if (trzba.EicPoverujiciho is not null
            && string.Equals(trzba.EicPoverujiciho, trzba.EicPopl, StringComparison.Ordinal))
        {
            warnings.Add("eic_poverujiciho se shoduje s eic_popl.");
        }

        if (trzba.DatTrzby > now + MaxSaleAheadOfNow)
        {
            warnings.Add("dat_trzby je o více než 2 hodiny v budoucnosti.");
        }

        if (trzba.DatTrzby < now.AddYears(-MaxSaleAgeYears))
        {
            warnings.Add("dat_trzby je starší než 2 roky.");
        }

        if (trzba.DatTrzby < MinimumSaleDate(environment, now))
        {
            warnings.Add("dat_trzby je dřív než minimální datum tržby pro zvolené prostředí.");
        }
    }

    private static bool HasRegisteredUnitShape(int idJednotky)
    {
        var lastDigit = idJednotky % 10;
        return idJednotky >= MinIdJednotkyWithTwoDigits && lastDigit is >= 1 and <= 4;
    }

    private static DateTimeOffset MinimumSaleDate(EetEnvironment environment, DateTimeOffset now) =>
        environment switch
        {
            EetEnvironment.Playground => PlaygroundMinimum,
            _ when now < ProductionStart => ProductionTransitionMinimum,
            _ => ProductionStart,
        };
}
