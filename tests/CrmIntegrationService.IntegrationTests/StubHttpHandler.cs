using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace CrmIntegrationService.IntegrationTests;

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? Body);

public sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, headers, body));
        return Respond(request);
    }

    // The test owns this instance; don't let IHttpClientFactory handler rotation dispose it.
    protected override void Dispose(bool disposing)
    {
    }
}
