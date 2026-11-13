using System.Globalization;
using System.Text;
using System.Xml;
using EetKit.Internal;
using EetKit.Tests;
using Xunit;

namespace EetKit.PlaygroundTests;

/// <summary>
/// Sends real messages to the EET playground. Runs only with EET_PLAYGROUND=1, because it needs
/// the network and the playground service (operational window: usually Thursday 20:00 to 6:00 is maintenance).
/// </summary>
public sealed class PlaygroundTests
{
    private const string EnabledVariable = "EET_PLAYGROUND";
    private const int IdJednotky = 303;
    private const int MaxMessageBytes = 12_288;

    private static bool Enabled => Environment.GetEnvironmentVariable(EnabledVariable) == "1";

    private static void SkipUnlessEnabled() =>
        Assert.SkipUnless(Enabled, $"Playground tests run only with {EnabledVariable}=1.");

    private static (EetClient Client, EetCertificate Certificate) Client(string eic)
    {
        var (p12, password) = SpecFiles.TestCertificate(eic);
        var certificate = EetCertificate.FromPkcs12(p12, password);
        var options = new EetClientOptions { Environment = EetEnvironment.Playground };
        return (new EetClient(new Provider(certificate), options, new HttpClient()), certificate);
    }

    // A fresh receipt number per run keeps each test sale distinct on the shared playground.
    private static Trzba Sale(string eic) => new(
        eic,
        IdJednotky,
        "EetKit/PG",
        "PG/" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture),
        DateTimeOffset.UtcNow,
        100.00m);

    [Theory]
    [InlineData("CZ00000019")]
    [InlineData("CZ683555118")]
    [InlineData("CZ8551015704")]
    public async Task Send_TestTaxpayer_Confirmed(string eic)
    {
        SkipUnlessEnabled();
        var (client, certificate) = Client(eic);
        using (certificate)
        {
            var result = await client.SendAsync(Sale(eic), cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Status == EetStatus.Confirmed, $"{result.Status} {result.ErrorCode} {result.ErrorText} ({result.GlobalTransactionId})");
            Assert.EndsWith("-ff", result.Pok, StringComparison.Ordinal);
            Assert.True(result.IsTest);
            Assert.NotNull(result.GlobalTransactionId);
        }
    }

    [Fact]
    public async Task Send_VerificationMode_VerifiedOnly()
    {
        SkipUnlessEnabled();
        var (client, certificate) = Client("CZ00000019");
        using (certificate)
        {
            var result = await client.SendAsync(
                Sale("CZ00000019"), new SendOptions(Overeni: true), TestContext.Current.CancellationToken);

            Assert.Equal(EetStatus.VerifiedOnly, result.Status);
            Assert.Equal(0, result.ErrorCode);
        }
    }

    [Fact]
    public async Task Send_BodyChangedAfterSigning_Rejected4()
    {
        SkipUnlessEnabled();
        var (client, certificate) = Client("CZ00000019");
        using (certificate)
        {
            var (envelope, uuid) = Signed(Sale("CZ00000019"), certificate);
            var data = envelope.GetElementsByTagName("Data", EetNamespaces.V4).OfType<XmlElement>().Single();
            data.SetAttribute("celk_trzba", "1.00");

            var result = await client.SendRawAsync(
                Encoding.UTF8.GetBytes(envelope.OuterXml), uuid, [], TestContext.Current.CancellationToken);

            Assert.Equal(EetStatus.Rejected, result.Status);
            Assert.Equal(4, result.ErrorCode);
        }
    }

    [Fact]
    public async Task Send_MessageOverLimit_Rejected7()
    {
        SkipUnlessEnabled();
        var (client, certificate) = Client("CZ00000019");
        using (certificate)
        {
            var (envelope, uuid) = Signed(Sale("CZ00000019"), certificate);
            const int CommentMarkupBytes = 7;
            var header = envelope.DocumentElement!["Header", EetNamespaces.Soap]!;
            var size = Encoding.UTF8.GetByteCount(envelope.OuterXml);
            header.PrependChild(envelope.CreateComment(new string('x', MaxMessageBytes + 1 - size - CommentMarkupBytes)));
            var bytes = Encoding.UTF8.GetBytes(envelope.OuterXml);
            Assert.Equal(MaxMessageBytes + 1, bytes.Length);

            var result = await client.SendRawAsync(bytes, uuid, [], TestContext.Current.CancellationToken);

            Assert.Equal(EetStatus.Rejected, result.Status);
            Assert.Equal(7, result.ErrorCode);
        }
    }

    private static (XmlDocument Envelope, Guid Uuid) Signed(Trzba trzba, EetCertificate certificate)
    {
        var header = new MessageHeader(Guid.NewGuid(), EetFormat.ToPragueTime(DateTimeOffset.UtcNow), PrvniZaslani: true, Overeni: false);
        var ids = SoapEnvelopeIds.CreateRandom();
        var envelope = SoapEnvelopeBuilder.Build(trzba with { DatTrzby = EetFormat.ToPragueTime(trzba.DatTrzby) }, header, ids);
        WsSecuritySigner.Sign(envelope, certificate, ids);
        return (envelope, header.UuidZpravy);
    }

    private sealed class Provider(EetCertificate certificate) : IEetCertificateProvider
    {
        public EetCertificate GetCertificate() => certificate;
    }
}
