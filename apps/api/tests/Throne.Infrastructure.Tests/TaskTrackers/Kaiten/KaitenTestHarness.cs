using System.Net;
using System.Text;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Http;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body);

/// <summary>
/// Records every outgoing request and replays a queued response per call, so the Kaiten client can
/// be exercised end-to-end (auth header, URL, retry loop, JSON mapping) without the network.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public StubHttpMessageHandler Enqueue(HttpStatusCode status, string? json = null) =>
        Enqueue(() =>
        {
            var response = new HttpResponseMessage(status);
            if (json is not null)
            {
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return response;
        });

    public StubHttpMessageHandler Enqueue(Func<HttpResponseMessage> factory)
    {
        _responses.Enqueue(factory);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("StubHttpMessageHandler ran out of queued responses.");
        }

        return _responses.Dequeue()();
    }
}

internal sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal static class KaitenTestHarness
{
    public static readonly KaitenConnection Connection = new("https://acme.kaiten.ru/", "secret-token");

    public static KaitenOptions FastOptions() => new()
    {
        RequestsPerSecond = 0,
        MaxAttempts = 3,
        RetryBaseDelayMilliseconds = 1,
        RetryMaxDelayMilliseconds = 1,
    };

    public static (KaitenHttpExecutor Executor, StubHttpMessageHandler Handler) NewExecutor(KaitenOptions? options = null)
    {
        options ??= FastOptions();
        var handler = new StubHttpMessageHandler();
        var factory = new FixedHttpClientFactory(new HttpClient(handler));
        var limiter = new KaitenRateLimiter(options, TimeProvider.System);
        var retry = new KaitenRetryPolicy(options);
        return (new KaitenHttpExecutor(factory, limiter, retry, options, TimeProvider.System), handler);
    }
}
