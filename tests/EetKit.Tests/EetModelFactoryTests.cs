using Xunit;

namespace EetKit.Tests;

public sealed class EetModelFactoryTests
{
    [Fact]
    public void EetModelFactory_Result_CarriesValues()
    {
        var uuid = Guid.Parse("03965780-6457-4842-bd80-5f9195c0b8c8");
        var received = new DateTimeOffset(2027, 1, 9, 16, 45, 36, TimeSpan.FromHours(1));
        var rejected = received.AddSeconds(1);
        EetWarning[] warnings = [new EetWarning(1, "DIC poplatnika v datove zprave se neshoduje s DIC v certifikatu")];
        string[] localWarnings = ["dat_trzby je starší než 2 roky."];

        var result = EetModelFactory.Result(
            EetStatus.Rejected,
            pok: "3efd94d7-2afb-401d-b213-986a62bcae55-ff",
            errorCode: 4,
            errorText: "Neplatny podpis SOAP zpravy",
            warnings: warnings,
            localWarnings: localWarnings,
            messageUuid: uuid,
            datPrij: received,
            datOdmit: rejected,
            isTest: true,
            globalTransactionId: "bc626a986aeddff902bee4c0");

        Assert.Equal(EetStatus.Rejected, result.Status);
        Assert.Equal("3efd94d7-2afb-401d-b213-986a62bcae55-ff", result.Pok);
        Assert.Equal(4, result.ErrorCode);
        Assert.Equal("Neplatny podpis SOAP zpravy", result.ErrorText);
        Assert.Equal(warnings, result.Warnings);
        Assert.Equal(localWarnings, result.LocalWarnings);
        Assert.Equal(uuid, result.MessageUuid);
        Assert.Equal(received, result.DatPrij);
        Assert.Equal(rejected, result.DatOdmit);
        Assert.True(result.IsTest);
        Assert.Equal("bc626a986aeddff902bee4c0", result.GlobalTransactionId);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void EetModelFactory_Result_Defaults()
    {
        var result = EetModelFactory.Result(EetStatus.Confirmed);

        Assert.Null(result.Pok);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorText);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.LocalWarnings);
        Assert.NotEqual(Guid.Empty, result.MessageUuid);
        Assert.Null(result.DatPrij);
        Assert.Null(result.DatOdmit);
        Assert.False(result.IsTest);
        Assert.Null(result.GlobalTransactionId);
    }

    [Theory]
    [InlineData(EetStatus.Confirmed, false)]
    [InlineData(EetStatus.VerifiedOnly, false)]
    [InlineData(EetStatus.TemporaryError, true)]
    [InlineData(EetStatus.Rejected, false)]
    public void EetModelFactory_Result_IsRetryableFollowsStatus(EetStatus status, bool expected)
    {
        Assert.Equal(expected, EetModelFactory.Result(status).IsRetryable);
    }
}
