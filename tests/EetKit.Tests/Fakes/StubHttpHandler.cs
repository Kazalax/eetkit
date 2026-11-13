using System.Net;

namespace EetKit.Tests.Fakes;

/// <summary>Stands in for the EET server: records each request and answers with a prepared response.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public List<RecordedRequest> Requests { get; } = [];

    public static StubHttpHandler Returning(byte[] body, HttpStatusCode status = HttpStatusCode.OK, string? transactionId = null) =>
        new((_, _) =>
        {
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
            if (transactionId is not null)
            {
                response.Headers.Add("X-Global-Transaction-ID", transactionId);
            }

            return Task.FromResult(response);
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Version,
            request.Headers.TryGetValues("SOAPAction", out var action) ? string.Join(",", action) : null,
            request.Content?.Headers.ContentType?.ToString(),
            body));
        return await _respond(request, cancellationToken);
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method, Uri? Uri, Version Version, string? SoapAction, string? ContentType, byte[] Body);
