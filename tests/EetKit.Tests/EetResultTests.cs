using Xunit;

namespace EetKit.Tests;

public sealed class EetResultTests
{
    [Theory]
    [InlineData(EetStatus.Confirmed, false)]
    [InlineData(EetStatus.VerifiedOnly, false)]
    [InlineData(EetStatus.TemporaryError, true)]
    [InlineData(EetStatus.Rejected, false)]
    public void IsRetryable_OnlyForTemporaryError(EetStatus status, bool expected)
    {
        var result = new EetResult(status, Guid.NewGuid(), []);

        Assert.Equal(expected, result.IsRetryable);
    }

    [Fact]
    public void Defaults_EmptyWarnings()
    {
        var result = new EetResult(EetStatus.Confirmed, Guid.NewGuid(), []);

        Assert.Empty(result.Warnings);
        Assert.Empty(result.LocalWarnings);
        Assert.Null(result.Pok);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Trzba_Constructor_SetsRequiredMembers()
    {
        var sale = new DateTimeOffset(2027, 1, 9, 16, 45, 36, TimeSpan.FromHours(1));

        var trzba = new Trzba("CZ00000019", 243, "24/A-6/Brno_2", "#135433c/11/2027", sale, 3264.50m)
        {
            CerpZuct = 10m,
        };

        Assert.Equal("CZ00000019", trzba.EicPopl);
        Assert.Equal(243, trzba.IdJednotky);
        Assert.Equal("24/A-6/Brno_2", trzba.IdPokl);
        Assert.Equal("#135433c/11/2027", trzba.PoradCis);
        Assert.Equal(sale, trzba.DatTrzby);
        Assert.Equal(3264.50m, trzba.CelkTrzba);
        Assert.Equal(10m, trzba.CerpZuct);
    }
}
