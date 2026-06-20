using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class OpencodeTuiClientTests
{
    [Fact(DisplayName = "CreateSessionAndSubmitAsync: create session → prompt_async, returns sessionID")]
    public async Task Create_session_then_submit_prompt_in_order()
    {
        var handler = new RecordingHandler();
        var client = new OpencodeTuiClient(new FixedHttpClientFactory(new HttpClient(handler)));

        var sessionId = await client.CreateSessionAndSubmitAsync(
            new Uri("http://127.0.0.1:4096/"), "/tmp/ws", "throne-local", "qwen-3", "TASK\nbody",
            CancellationToken.None);

        sessionId.Should().Be("ses_test");
        handler.Requests.Select(r => (r.Method, r.PathAndQuery)).Should().Equal(
            (HttpMethod.Post, "/session?directory=%2Ftmp%2Fws"),
            (HttpMethod.Post, "/session/ses_test/prompt_async?directory=%2Ftmp%2Fws"));
    }

    [Fact(DisplayName = "prompt_async pins model {providerID,modelID} and the text part")]
    public async Task Prompt_body_pins_model_and_parts()
    {
        var handler = new RecordingHandler();
        var client = new OpencodeTuiClient(new FixedHttpClientFactory(new HttpClient(handler)));

        await client.CreateSessionAndSubmitAsync(
            new Uri("http://127.0.0.1:4096/"), "/tmp/ws", "throne-local", "qwen-3", "TASK\nbody",
            CancellationToken.None);

        var promptBody = handler.Requests.Single(r => r.PathAndQuery.Contains("prompt_async")).Body;
        promptBody.Should().Contain("\"model\":{\"providerID\":\"throne-local\",\"modelID\":\"qwen-3\"}");
        promptBody.Should().Contain("\"parts\":[{\"type\":\"text\",\"text\":\"TASK\\nbody\"}]");
    }

    [Fact(DisplayName = "Сбой создания сессии пробрасывается как ошибка")]
    public async Task Session_create_failure_throws()
    {
        var handler = new RecordingHandler { SessionCreateStatus = HttpStatusCode.InternalServerError };
        var client = new OpencodeTuiClient(new FixedHttpClientFactory(new HttpClient(handler)));

        var act = () => client.CreateSessionAndSubmitAsync(
            new Uri("http://127.0.0.1:4096/"), "/tmp/ws", "throne-local", "qwen-3", "TASK",
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Requests.Should().ContainSingle();
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed record RequestSnapshot(HttpMethod Method, string PathAndQuery, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];
        public HttpStatusCode SessionCreateStatus { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(request.Method, path, body));

            if (path.StartsWith("/session?", StringComparison.Ordinal))
            {
                return SessionCreateStatus == HttpStatusCode.OK
                    ? Json(new { id = "ses_test", title = "untitled" })
                    : new HttpResponseMessage(SessionCreateStatus);
            }

            if (path.Contains("/prompt_async", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return Json(true);
        }

        private static HttpResponseMessage Json(object value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
