using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeTuiClientTests
{
    [Fact(DisplayName = "SubmitInitialPromptAsync ждёт health, append-ит prompt JSON и submit-ит composer")]
    public async Task Submit_initial_prompt_calls_tui_api_in_order()
    {
        var handler = new RecordingHandler();
        var client = new OpencodeTuiClient(
            new FixedHttpClientFactory(new HttpClient(handler)),
            new RunPreflightOptions
            {
                TuiReadinessTimeoutMilliseconds = 200,
                TuiReadinessPollIntervalMilliseconds = 20,
            },
            TimeProvider.System);

        await client.SubmitInitialPromptAsync(
            new Uri("http://127.0.0.1:49152/"), "/tmp/ws", "TASK\nbody", CancellationToken.None);

        handler.Requests.Select(r => (r.Method, r.PathAndQuery)).Should().Equal(
            (HttpMethod.Get, "/global/health"),
            (HttpMethod.Post, "/tui/append-prompt?directory=%2Ftmp%2Fws"),
            (HttpMethod.Post, "/tui/submit-prompt?directory=%2Ftmp%2Fws"));
        handler.Requests[1].Body.Should().Contain("\"text\":\"TASK\\nbody\"");
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed record RequestSnapshot(HttpMethod Method, string PathAndQuery, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri!.PathAndQuery,
                body));

            if (request.RequestUri.PathAndQuery == "/global/health")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { healthy = true, version = "1.17.7" }),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(true),
            };
        }
    }
}
