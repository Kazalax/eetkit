using System.Globalization;
using System.Text;
using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

public sealed class ResponseParserTests
{
    // uuid_zpravy of the sample request that produced both saved responses.
    private static readonly Guid SentUuid = Guid.Parse("03965780-6457-4842-bd80-5f9195c0b8c8");

    private static ResponseContext Context(Guid? sentUuid = null, EetEnvironment environment = EetEnvironment.Playground) =>
        new(sentUuid ?? SentUuid, "bc626a986aeddff902bee4c0", ["místní varování"], Fixtures.CapturedAt, environment);

    private static byte[] ErrorResponse(int code, string extra = "") => Encoding.UTF8.GetBytes(
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:eet=\"http://fs.gov.cz/eet/schema/v4\">" +
        "<soapenv:Header/><soapenv:Body><eet:Odpoved>" +
        "<eet:Hlavicka uuid_zpravy=\"03965780-6457-4842-bd80-5f9195c0b8c8\" dat_odmit=\"2026-11-06T15:12:03+01:00\"/>" +
        $"<eet:Chyba kod=\"{code.ToString(CultureInfo.InvariantCulture)}\" test=\"true\">Text chyby</eet:Chyba>{extra}" +
        "</eet:Odpoved></soapenv:Body></soapenv:Envelope>");

    [Fact]
    public void PlaygroundConfirmation_Confirmed()
    {
        var result = ResponseParser.Parse(Fixtures.Bytes(Fixtures.Potvrzeni), Context());

        Assert.Equal(EetStatus.Confirmed, result.Status);
        Assert.Equal("3efd94d7-2afb-401d-b213-986a62bcae55-ff", result.Pok);
        Assert.True(result.IsTest);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 15, 11, 5, TimeSpan.FromHours(1)), result.DatPrij);
        Assert.Equal(SentUuid, result.MessageUuid);
        Assert.Equal("bc626a986aeddff902bee4c0", result.GlobalTransactionId);
        Assert.Equal(["místní varování"], result.LocalWarnings);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void PlaygroundConfirmation_TamperedPok_TemporaryError()
    {
        var body = Encoding.UTF8.GetString(Fixtures.Bytes(Fixtures.Potvrzeni))
            .Replace("3efd94d7-2afb-401d-b213-986a62bcae55-ff", "00000000-0000-4000-8000-000000000000-ff", StringComparison.Ordinal);

        var result = ResponseParser.Parse(Encoding.UTF8.GetBytes(body), Context());

        Assert.Equal(EetStatus.TemporaryError, result.Status);
        Assert.Null(result.Pok);
    }

    [Fact]
    public void PlaygroundConfirmation_InProductionClient_TemporaryError()
    {
        var result = ResponseParser.Parse(Fixtures.Bytes(Fixtures.Potvrzeni), Context(environment: EetEnvironment.Production));

        Assert.Equal(EetStatus.TemporaryError, result.Status);
        Assert.Null(result.Pok);
    }

    [Fact]
    public void PlaygroundError4_Rejected()
    {
        var result = ResponseParser.Parse(Fixtures.Bytes(Fixtures.Chyba4), Context());

        Assert.Equal(EetStatus.Rejected, result.Status);
        Assert.Equal(4, result.ErrorCode);
        Assert.Equal("Neplatny podpis SOAP zpravy", result.ErrorText);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 15, 12, 3, TimeSpan.FromHours(1)), result.DatOdmit);
        Assert.True(result.IsTest);
    }

    [Fact]
    public void PlaygroundError4_OtherPrefix_SameResult()
    {
        var body = Encoding.UTF8.GetString(Fixtures.Bytes(Fixtures.Chyba4))
            .Replace("eet:", "v4:", StringComparison.Ordinal)
            .Replace("xmlns:eet=", "xmlns:v4=", StringComparison.Ordinal);

        var result = ResponseParser.Parse(Encoding.UTF8.GetBytes(body), Context());

        Assert.Equal(EetStatus.Rejected, result.Status);
        Assert.Equal(4, result.ErrorCode);
    }

    [Theory]
    [InlineData(-1, EetStatus.TemporaryError)]
    [InlineData(0, EetStatus.VerifiedOnly)]
    [InlineData(2, EetStatus.Rejected)]
    [InlineData(3, EetStatus.Rejected)]
    [InlineData(4, EetStatus.Rejected)]
    [InlineData(5, EetStatus.Rejected)]
    [InlineData(6, EetStatus.Rejected)]
    [InlineData(7, EetStatus.Rejected)]
    [InlineData(8, EetStatus.TemporaryError)]
    [InlineData(-5, EetStatus.TemporaryError)]
    [InlineData(1, EetStatus.Rejected)]
    [InlineData(9, EetStatus.Rejected)]
    public void ErrorCodes_Classified(int code, EetStatus expected)
    {
        var result = ResponseParser.Parse(ErrorResponse(code), Context());

        Assert.Equal(expected, result.Status);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal("Text chyby", result.ErrorText);
    }

    [Fact]
    public void Warnings_Parsed()
    {
        var warnings =
            "<eet:Varovani kod_varov=\"1\">EIC poplatnika v datove zprave se neshoduje s EIC v certifikatu</eet:Varovani>" +
            "<eet:Varovani kod_varov=\"6\"> id_jednotky neodpovida formatem </eet:Varovani>";

        var result = ResponseParser.Parse(ErrorResponse(0, warnings), Context());

        Assert.Equal(
            [
                new EetWarning(1, "EIC poplatnika v datove zprave se neshoduje s EIC v certifikatu"),
                new EetWarning(6, "id_jednotky neodpovida formatem"),
            ],
            result.Warnings);
    }

    [Fact]
    public void DifferentUuid_TemporaryError()
    {
        var result = ResponseParser.Parse(Fixtures.Bytes(Fixtures.Chyba4), Context(Guid.NewGuid()));

        Assert.Equal(EetStatus.TemporaryError, result.Status);
    }

    [Fact]
    public void SoapFault_TemporaryError()
    {
        var fault = Encoding.UTF8.GetBytes(
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body>" +
            "<soapenv:Fault><faultcode>soapenv:Server</faultcode><faultstring>Internal error</faultstring></soapenv:Fault>" +
            "</soapenv:Body></soapenv:Envelope>");

        var result = ResponseParser.Parse(fault, Context());

        Assert.Equal(EetStatus.TemporaryError, result.Status);
        Assert.Contains("Internal error", result.ErrorText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body/></soapenv:Envelope>")]
    public void UnreadableResponse_TemporaryError(string body)
    {
        var result = ResponseParser.Parse(Encoding.UTF8.GetBytes(body), Context());

        Assert.Equal(EetStatus.TemporaryError, result.Status);
        Assert.Equal(SentUuid, result.MessageUuid);
    }

    [Fact]
    public void ExternalEntity_NotResolved()
    {
        const string Secret = "TAJNY-OBSAH-SOUBORU";
        var secretFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"eetkit-xxe-{Guid.NewGuid():N}.txt");
        File.WriteAllText(secretFile, Secret);
        try
        {
            var body = Encoding.UTF8.GetBytes(
                $"<?xml version=\"1.0\"?><!DOCTYPE r [<!ENTITY x SYSTEM \"{new Uri(secretFile).AbsoluteUri}\">]>" +
                "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body>" +
                "<soapenv:Fault><faultstring>&x;</faultstring></soapenv:Fault></soapenv:Body></soapenv:Envelope>");

            var result = ResponseParser.Parse(body, Context());

            Assert.Equal(EetStatus.TemporaryError, result.Status);
            Assert.DoesNotContain(Secret, result.ErrorText ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(secretFile);
        }
    }
}
