using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeTuiClientTests
{
    [Fact(DisplayName = "SubmitInitialPromptAsync: health → create session → prompt_async → select-session")]
    public async Task Submit_initial_prompt_calls_server_session_api_in_order()
    {
        var handler = new RecordingHandler();
        var client = NewClient(handler);

        await client.SubmitInitialPromptAsync(
            new Uri("http://127.0.0.1:49152/"), "/tmp/ws", "TASK\nbody", CancellationToken.None);

        handler.Requests.Select(r => (r.Method, r.PathAndQuery)).Should().Equal(
            (HttpMethod.Get, "/global/health"),
            (HttpMethod.Post, "/session?directory=%2Ftmp%2Fws"),
            (HttpMethod.Post, "/session/ses_test/prompt_async?directory=%2Ftmp%2Fws"),
            (HttpMethod.Post, "/tui/select-session?directory=%2Ftmp%2Fws"));

        handler.Requests[2].Body.Should().Contain("\"parts\":[{\"type\":\"text\",\"text\":\"TASK\\nbody\"}]");
        handler.Requests[3].Body.Should().Contain("\"sessionID\":\"ses_test\"");
    }

    [Fact(DisplayName = "select-session ретраится, пока TUI-фронт не примет фокус")]
    public async Task Select_session_is_retried_until_tui_accepts()
    {
        var handler = new RecordingHandler { SelectSessionFalseUntilAttempt = 3 };
        var client = NewClient(handler);

        await client.SubmitInitialPromptAsync(
            new Uri("http://127.0.0.1:49152/"), "/tmp/ws", "TASK", CancellationToken.None);

        handler.SelectSessionAttempts.Should().Be(3);
    }

    [Fact(DisplayName = "select-session best-effort: сессия создана, провал фокуса не валит спавн")]
    public async Task Select_session_failure_does_not_fail_the_spawn()
    {
        var handler = new RecordingHandler { SelectSessionFalseUntilAttempt = int.MaxValue };
        var client = NewClient(handler);

        var act = () => client.SubmitInitialPromptAsync(
            new Uri("http://127.0.0.1:49152/"), "/tmp/ws", "TASK", CancellationToken.None);

        await act.Should().NotThrowAsync();
        handler.Requests.Select(r => r.PathAndQuery).Should().Contain(
            "/session?directory=%2Ftmp%2Fws",
            "/session/ses_test/prompt_async?directory=%2Ftmp%2Fws");
        handler.SelectSessionAttempts.Should().BeGreaterThan(1);
    }

    private static OpencodeTuiClient NewClient(RecordingHandler handler) =>
        new(
            new FixedHttpClientFactory(new HttpClient(handler)),
            new RunPreflightOptions
            {
                TuiReadinessTimeoutMilliseconds = 200,
                TuiReadinessPollIntervalMilliseconds = 20,
            },
            TimeProvider.System,
            NullLogger<OpencodeTuiClient>.Instance);

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed record RequestSnapshot(HttpMethod Method, string PathAndQuery, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];
        public int SelectSessionFalseUntilAttempt { get; init; }
        public int SelectSessionAttempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(request.Method, path, body));

            if (path == "/global/health")
            {
                return Json(new { healthy = true, version = "1.17.7" });
            }

            if (path.StartsWith("/session?", StringComparison.Ordinal))
            {
                return Json(new { id = "ses_test", title = "untitled" });
            }

            if (path.Contains("/prompt_async", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.StartsWith("/tui/select-session", StringComparison.Ordinal))
            {
                SelectSessionAttempts++;
                return Json(SelectSessionAttempts >= SelectSessionFalseUntilAttempt);
            }

            return Json(true);
        }

        private static HttpResponseMessage Json(object value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
