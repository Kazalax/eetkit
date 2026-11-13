using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using EetKit.Internal;

namespace EetKit;

/// <summary>
/// Posílá tržby do EET 2.0. Jedna instance se dá sdílet mezi vlákny po celou dobu běhu aplikace.
/// </summary>
public sealed class EetClient : IDisposable
{
    private const string SoapAction = "\"http://fs.gov.cz/eet/OdeslaniTrzby\"";
    private const string TransactionIdHeader = "X-Global-Transaction-Id";
    private const string ContentType = "text/xml; charset=utf-8";

    // The production environment balances by DNS; new connections must ask DNS again.
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    private readonly IEetCertificateProvider _certificates;
    private readonly EetClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly Uri _endpoint;
    private readonly IDisposable? _owned;

    /// <summary>Vytvoří klienta nad dodaným <see cref="HttpClient"/>, který klient neuvolňuje.</summary>
    /// <param name="certificates">Zdroj certifikátu poplatníka.</param>
    /// <param name="options">Prostředí a timeout.</param>
    /// <param name="httpClient">HTTP klient, například z <c>IHttpClientFactory</c>.</param>
    /// <param name="timeProvider">Zdroj času, výchozí je systémový.</param>
    /// <exception cref="ArgumentOutOfRangeException">Timeout je kratší než <see cref="EetClientOptions.MinimumTimeout"/>.</exception>
    public EetClient(
        IEetCertificateProvider certificates,
        EetClientOptions options,
        HttpClient httpClient,
        TimeProvider? timeProvider = null)
        : this(certificates, options, httpClient, timeProvider, owned: null)
    {
    }

    private EetClient(
        IEetCertificateProvider certificates,
        EetClientOptions options,
        HttpClient httpClient,
        TimeProvider? timeProvider,
        IDisposable? owned)
    {
        Guard.NotNull(certificates, nameof(certificates));
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(httpClient, nameof(httpClient));
        if (options.Timeout < EetClientOptions.MinimumTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), options.Timeout, "Timeout musí být aspoň 2 sekundy.");
        }

        _certificates = certificates;
        _options = options;
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _endpoint = EetEndpoints.For(options.Environment);
        _owned = owned;
    }

    /// <summary>Source of uuid_zpravy; replaced in tests to answer saved responses.</summary>
    internal Func<Guid> NewMessageUuid { get; set; } = Guid.NewGuid;

    /// <summary>
    /// Vytvoří klienta s certifikátem ze souboru .p12 a vlastním HTTP klientem. Klienta je potřeba uvolnit.
    /// </summary>
    /// <exception cref="EetCertificateException">Certifikát nejde načíst.</exception>
    public static EetClient FromPkcs12(string path, string password, EetEnvironment environment)
    {
        var provider = new FixedCertificateProvider(EetCertificate.FromPkcs12File(path, password));
        var httpClient = CreateHttpClient();
        var owned = new CompositeDisposable(httpClient, provider);
        return new EetClient(provider, new EetClientOptions { Environment = environment }, httpClient, null, owned);
    }

    /// <summary>
    /// Zkontroluje, podepíše a odešle tržbu. Každé volání je nová zpráva s novým <c>uuid_zpravy</c>.
    /// </summary>
    /// <param name="trzba">Údaje o tržbě.</param>
    /// <param name="options">První zaslání nebo opakování, ověřovací režim.</param>
    /// <param name="cancellationToken">Zrušení volajícím vyhodí <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="EetValidationException">Data tržby nejsou platná. Nic se neodeslalo.</exception>
    public async Task<EetResult> SendAsync(Trzba trzba, SendOptions? options = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(trzba, nameof(trzba));
        options ??= new SendOptions();

        var now = _timeProvider.GetUtcNow();
        var validation = TrzbaValidator.Validate(trzba, _options.Environment, now);
        if (validation.Errors.Count > 0)
        {
            throw new EetValidationException(validation.Errors);
        }

        var certificate = _certificates.GetCertificate();
        var localWarnings = validation.Warnings.Concat(certificate.GetWarnings(trzba.EicPopl, now)).ToList();

        var header = new MessageHeader(NewMessageUuid(), EetFormat.ToPragueTime(now), options.PrvniZaslani, options.Overeni);
        var ids = SoapEnvelopeIds.CreateRandom();
        var envelope = SoapEnvelopeBuilder.Build(trzba with { DatTrzby = EetFormat.ToPragueTime(trzba.DatTrzby) }, header, ids);
        WsSecuritySigner.Sign(envelope, certificate, ids);

        return await SendRawAsync(EnvelopeSerializer.ToUtf8(envelope), header.UuidZpravy, localWarnings, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sends already built bytes; used directly by the playground tests for deliberately broken messages.</summary>
    internal async Task<EetResult> SendRawAsync(
        byte[] envelope, Guid uuid, IReadOnlyList<string> localWarnings, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Version = HttpVersion.Version11,
            Content = new ByteArrayContent(envelope),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentType);
        request.Headers.TryAddWithoutValidation("SOAPAction", SoapAction);

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
            var transactionId = response.Headers.TryGetValues(TransactionIdHeader, out var values) ? values.FirstOrDefault() : null;

            var status = (int)response.StatusCode;
            if (IsTemporaryHttpStatus(status))
            {
                return Failure(EetStatus.TemporaryError, uuid, localWarnings, transactionId, HttpStatusText(status));
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return Failure(EetStatus.Rejected, uuid, localWarnings, transactionId, HttpStatusText(status));
            }

            var body = await ReadBodyAsync(response, timeout.Token).ConfigureAwait(false);
            var context = new ResponseContext(uuid, transactionId, localWarnings, _timeProvider.GetUtcNow(), _options.Environment);
            return ResponseParser.Parse(body, context);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(EetStatus.TemporaryError, uuid, localWarnings, null, string.Format(
                CultureInfo.InvariantCulture, "Server neodpověděl do {0} s.", _options.Timeout.TotalSeconds));
        }
        catch (HttpRequestException ex)
        {
            return Failure(EetStatus.TemporaryError, uuid, localWarnings, null, "Spojení se serverem selhalo: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _owned?.Dispose();

    private static bool IsTemporaryHttpStatus(int status) =>
        status is (int)HttpStatusCode.RequestTimeout or 429 || status >= (int)HttpStatusCode.InternalServerError;

    private static string HttpStatusText(int status) =>
        string.Format(CultureInfo.InvariantCulture, "Server odpověděl HTTP {0}.", status);

    private static EetResult Failure(
        EetStatus status, Guid uuid, IReadOnlyList<string> localWarnings, string? transactionId, string text) =>
        new(status, uuid, localWarnings) { ErrorText = text, GlobalTransactionId = transactionId };

    private static async Task<byte[]> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif
    }

    private static HttpClient CreateHttpClient()
    {
#if NET5_0_OR_GREATER
        return new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = PooledConnectionLifetime });
#else
        // TODO: on .NET Framework DNS changes are honoured via ServicePointManager.DnsRefreshTimeout
        // (default 2 min) and ConnectionLeaseTimeout; verify behaviour once net48 tests exist.
        return new HttpClient(new HttpClientHandler());
#endif
    }

    private sealed class CompositeDisposable(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
    }
}
