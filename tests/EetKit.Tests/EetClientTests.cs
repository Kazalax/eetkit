using System.Net;
using System.Text;
using System.Xml;
using EetKit.Internal;
using EetKit.Tests.Fakes;
using Xunit;

namespace EetKit.Tests;

public sealed class EetClientTests : IDisposable
{
    private const string Eic = "CZ00000019";

    // The saved playground responses answer this message id.
    private static readonly Guid FixtureUuid = Guid.Parse("03965780-6457-4842-bd80-5f9195c0b8c8");

    private readonly EetCertificate _certificate;
    private readonly Trzba _trzba = SampleRequest.Load(Eic).Trzba;

    public EetClientTests()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        _certificate = EetCertificate.FromPkcs12(p12, password);
    }

    public void Dispose() => _certificate.Dispose();

    private EetClient Client(StubHttpHandler handler, TimeSpan? timeout = null, bool fixtureUuid = true)
    {
        var options = new EetClientOptions { Environment = EetEnvironment.Playground, Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        var client = new EetClient(new Provider(_certificate), options, new HttpClient(handler), new FixedTimeProvider(Fixtures.CapturedAt));
        if (fixtureUuid)
        {
            client.NewMessageUuid = () => FixtureUuid;
        }

        return client;
    }

    private static XmlDocument SentDocument(StubHttpHandler handler, int index = 0)
    {
        return TestXml.FromString(Encoding.UTF8.GetString(handler.Requests[index].Body));
    }

    private static XmlElement SentElement(StubHttpHandler handler, string localName, int index = 0) =>
        SentDocument(handler, index).GetElementsByTagName(localName, EetNamespaces.V4).OfType<XmlElement>().Single();

    [Fact]
    public async Task Send_ServerConfirms_Confirmed()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Potvrzeni), transactionId: "bc626a986aeddff902bee4c0");

        var result = await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EetStatus.Confirmed, result.Status);
        Assert.Equal("3efd94d7-2afb-401d-b213-986a62bcae55-ff", result.Pok);
        Assert.Equal("bc626a986aeddff902bee4c0", result.GlobalTransactionId);
        Assert.Equal(FixtureUuid, result.MessageUuid);
    }

    [Fact]
    public async Task Send_ServerRejects_Rejected()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        var result = await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EetStatus.Rejected, result.Status);
        Assert.Equal(4, result.ErrorCode);
    }

    [Fact]
    public async Task Send_Request_HasSoapHeaders()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://pg.trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4"), request.Uri);
        Assert.Equal(HttpVersion.Version11, request.Version);
        Assert.Equal("\"http://fs.gov.cz/eet/OdeslaniTrzby\"", request.SoapAction);
        Assert.Equal("text/xml; charset=utf-8", request.ContentType);
    }

    [Fact]
    public async Task Send_Body_SignedUtf8WithoutBom()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal((byte)'<', handler.Requests[0].Body[0]);
        Assert.True(WsSecurityVerifier.Verify(SentDocument(handler)));
    }

    [Fact]
    public async Task Send_Times_InCzechLocalTime()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("2026-11-06T15:11:05+01:00", SentElement(handler, "Hlavicka").GetAttribute("dat_odesl"));
        Assert.Equal("2026-07-01T11:02:18+02:00", SentElement(handler, "Data").GetAttribute("dat_trzby"));
    }

    [Fact]
    public async Task TwoSends_DifferentUuid()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));
        var client = Client(handler, fixtureUuid: false);

        await client.SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);
        await client.SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(
            SentElement(handler, "Hlavicka", 0).GetAttribute("uuid_zpravy"),
            SentElement(handler, "Hlavicka", 1).GetAttribute("uuid_zpravy"));
    }

    [Fact]
    public async Task Send_Options_WrittenToHeader()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        await Client(handler).SendAsync(_trzba, new SendOptions(PrvniZaslani: false, Overeni: true), TestContext.Current.CancellationToken);

        var hlavicka = SentElement(handler, "Hlavicka");
        Assert.Equal("false", hlavicka.GetAttribute("prvni_zaslani"));
        Assert.Equal("true", hlavicka.GetAttribute("overeni"));
    }

    [Fact]
    public async Task InvalidData_ThrowsAndSendsNothing()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        await Assert.ThrowsAsync<EetValidationException>(() =>
            Client(handler).SendAsync(_trzba with { IdPokl = "Pokladna č.1" }, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ServerTooSlow_TemporaryError()
    {
        var handler = new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await Client(handler, TimeSpan.FromSeconds(2)).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EetStatus.TemporaryError, result.Status);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public async Task NetworkFailure_TemporaryError()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("Name or service not known"));

        var result = await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EetStatus.TemporaryError, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, EetStatus.TemporaryError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, EetStatus.TemporaryError)]
    [InlineData((HttpStatusCode)429, EetStatus.TemporaryError)] // TooManyRequests; the name is missing on .NET Framework
    [InlineData(HttpStatusCode.RequestTimeout, EetStatus.TemporaryError)]
    [InlineData(HttpStatusCode.NotFound, EetStatus.TemporaryError)]
    [InlineData(HttpStatusCode.MethodNotAllowed, EetStatus.TemporaryError)]
    [InlineData(HttpStatusCode.Forbidden, EetStatus.TemporaryError)]
    [InlineData(HttpStatusCode.ProxyAuthenticationRequired, EetStatus.TemporaryError)]
    public async Task HttpStatus_Classified(HttpStatusCode status, EetStatus expected)
    {
        var handler = StubHttpHandler.Returning([], status);

        var result = await Client(handler).SendAsync(_trzba, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task CallerCancels_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpHandler(async (_, token) =>
        {
#if NETFRAMEWORK
            cancellation.Cancel();
#else
            await cancellation.CancelAsync();
#endif
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(handler).SendAsync(_trzba, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task LocalWarnings_Reported()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));

        var result = await Client(handler).SendAsync(_trzba with { IdJednotky = 305 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.LocalWarnings);
    }

    [Fact]
    public void TimeoutBelowMinimum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Client(StubHttpHandler.Returning([]), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Production_UsesProductionEndpoint()
    {
        Assert.Equal(new Uri("https://trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4"), EetEndpoints.For(EetEnvironment.Production));
    }

    private sealed class Provider(EetCertificate certificate) : IEetCertificateProvider
    {
        public EetCertificate GetCertificate() => certificate;
    }
}
