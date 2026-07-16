using System.Net;
using System.Text;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.GenericHttp;

namespace Throne.Infrastructure.Tests.TaskTrackers.GenericHttp;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization);

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

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString()));
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("Stub HTTP handler ran out of responses.");
        }
        return Task.FromResult(_responses.Dequeue()());
    }
}

internal sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal static class GenericHttpTestHarness
{
    public static readonly TaskTrackerConnectionDescriptor Descriptor =
        new("https://tasks.example.test/", "secret-token");

    public static (GenericHttpTaskTrackerProvider Provider, StubHttpMessageHandler Handler) NewProvider()
    {
        var handler = new StubHttpMessageHandler();
        var factory = new FixedHttpClientFactory(new HttpClient(handler));
        return (new GenericHttpTaskTrackerProvider(new GenericHttpClient(factory)), handler);
    }
}
