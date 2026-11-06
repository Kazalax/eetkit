using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

public sealed class TrzbaValidatorTests
{
    private static readonly DateTimeOffset SaleTime = new(2026, 7, 1, 9, 2, 18, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = SaleTime.AddMinutes(1);

    // Data of the official sample request CZ00000019.eet.v4.req.xml.
    private static Trzba ValidTrzba() => new()
    {
        EicPopl = "CZ00000019",
        IdJednotky = 303,
        IdPokl = "/5604/MA65",
        PoradCis = "00/2224/SO57",
        DatTrzby = SaleTime,
        CelkTrzba = 188580.00m,
        UrcenoCerpZuct = 25.00m,
        CerpZuct = 302.00m,
    };

    private static TrzbaValidationResult Validate(
        Trzba trzba,
        EetEnvironment environment = EetEnvironment.Playground,
        DateTimeOffset? now = null) =>
        TrzbaValidator.Validate(trzba, environment, now ?? Now);

    private static string ErrorField(TrzbaValidationResult result) => Assert.Single(result.Errors).Field;

    [Fact]
    public void SampleData_NoErrorsNoWarnings()
    {
        var result = Validate(ValidTrzba());

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("CZ1234567")]
    [InlineData("SK12345678")]
    [InlineData("CZ12345678901")]
    [InlineData("CZ12345678\n")]
    [InlineData("")]
    public void EicPopl_Invalid_Error(string eic)
    {
        Assert.Equal("eic_popl", ErrorField(Validate(ValidTrzba() with { EicPopl = eic })));
    }

    [Fact]
    public void EicPoverujiciho_Invalid_Error()
    {
        Assert.Equal("eic_poverujiciho", ErrorField(Validate(ValidTrzba() with { EicPoverujiciho = "" })));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000000000)]
    public void IdJednotky_OutOfRange_Error(int idJednotky)
    {
        Assert.Equal("id_jednotky", ErrorField(Validate(ValidTrzba() with { IdJednotky = idJednotky })));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789012345678901")]
    [InlineData("Pokladna č.1")]
    public void IdPokl_Invalid_Error(string idPokl)
    {
        Assert.Equal("id_pokl", ErrorField(Validate(ValidTrzba() with { IdPokl = idPokl })));
    }

    [Theory]
    [InlineData("12345678901234567890123456")]
    [InlineData("Účtenka 1")]
    [InlineData("A\tB")]
    public void PoradCis_Invalid_Error(string poradCis)
    {
        Assert.Equal("porad_cis", ErrorField(Validate(ValidTrzba() with { PoradCis = poradCis })));
    }

    [Theory]
    [InlineData("100000000")]
    [InlineData("-100000000")]
    [InlineData("12.345")]
    public void CelkTrzba_Invalid_Error(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("celk_trzba", ErrorField(Validate(ValidTrzba() with { CelkTrzba = value })));
    }

    [Fact]
    public void OptionalAmounts_Invalid_Error()
    {
        var result = Validate(ValidTrzba() with { UrcenoCerpZuct = 0.001m, CerpZuct = 100000000m });

        Assert.Equal(["urceno_cerp_zuct", "cerp_zuct"], result.Errors.Select(e => e.Field));
    }

    [Fact]
    public void RequiredTextMissing_Error()
    {
        var result = Validate(ValidTrzba() with { EicPopl = null!, IdPokl = null!, PoradCis = null! });

        Assert.Equal(["eic_popl", "id_pokl", "porad_cis"], result.Errors.Select(e => e.Field));
    }

    [Fact]
    public void AllErrors_ReturnedAtOnce()
    {
        var result = Validate(ValidTrzba() with { EicPopl = "X", IdJednotky = 0, IdPokl = "", CelkTrzba = 1.001m });

        Assert.Equal(4, result.Errors.Count);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(305)]
    [InlineData(300)]
    public void IdJednotky_UnusualShape_Warning(int idJednotky)
    {
        var result = Validate(ValidTrzba() with { IdJednotky = idJednotky });

        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void EicPoverujiciho_SameAsPopl_Warning()
    {
        var result = Validate(ValidTrzba() with { EicPoverujiciho = "CZ00000019" });

        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void DatTrzby_MoreThanTwoHoursAhead_Warning()
    {
        var result = Validate(ValidTrzba(), now: SaleTime.AddHours(-3));

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void DatTrzby_OlderThanTwoYears_Warning()
    {
        var result = Validate(ValidTrzba(), now: SaleTime.AddYears(2).AddDays(1));

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Playground_BeforeMinimumDate_Warning()
    {
        var sale = new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.FromHours(2));

        var result = Validate(ValidTrzba() with { DatTrzby = sale }, now: sale.AddMinutes(1));

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Production_Transition_BeforeNovemberFirst_Warning()
    {
        var sale = new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.FromHours(1));

        var result = Validate(ValidTrzba() with { DatTrzby = sale }, EetEnvironment.Production, sale.AddMinutes(1));

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Production_Transition_AfterNovemberFirst_NoWarning()
    {
        var sale = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.FromHours(1));

        var result = Validate(ValidTrzba() with { DatTrzby = sale }, EetEnvironment.Production, sale.AddMinutes(1));

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Production_From2027_BeforeJanuaryFirst_Warning()
    {
        var sale = new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.FromHours(1));

        var result = Validate(ValidTrzba() with { DatTrzby = sale }, EetEnvironment.Production, sale.AddHours(2));

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void ValidationException_ListsFields()
    {
        var errors = new[] { new EetValidationError("eic_popl", "x"), new EetValidationError("id_pokl", "y") };

        var exception = new EetValidationException(errors);

        Assert.Equal(errors, exception.Errors);
        Assert.Contains("eic_popl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("id_pokl", exception.Message, StringComparison.Ordinal);
    }
}
