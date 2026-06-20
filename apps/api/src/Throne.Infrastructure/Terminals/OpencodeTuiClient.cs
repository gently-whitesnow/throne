using System.Net.Http.Json;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Creates the OpenCode session and delivers the initial prompt through the shared serve's HTTP
/// server API (<c>POST /session</c>, <c>POST /session/{id}/prompt_async</c>). These endpoints are
/// authoritative the moment <c>/global/health</c> returns 200 — they create and run the session in
/// the headless <c>opencode serve</c> regardless of whether any front is attached. The model is
/// pinned on the prompt body (<c>model={providerID,modelID}</c>) because the attach front carries
/// no model flag and the serve has no per-launch default.
///
/// No TUI command-bus call: the operator front pulls the session by id via
/// <c>opencode attach … --session</c>, so the old <c>tui/select-session</c> focus race is gone.
/// </summary>
internal sealed class OpencodeTuiClient(IHttpClientFactory httpClientFactory) : IOpencodeTuiClient
{
    public const string HttpClientName = "opencode-tui";

    public async Task<string> CreateSessionAndSubmitAsync(
        Uri endpoint,
        string workspacePath,
        string providerId,
        string modelId,
        string prompt,
        CancellationToken ct)
    {
        var sessionId = await CreateSessionAsync(endpoint, workspacePath, ct);
        await SendPromptAsync(endpoint, workspacePath, sessionId, providerId, modelId, prompt, ct);
        return sessionId;
    }

    private async Task<string> CreateSessionAsync(Uri endpoint, string workspacePath, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, BuildUri(endpoint, "session", workspacePath));
        OpencodeServerAuth.Apply(request);
        using var response = await CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenCode session create returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken: ct);
        if (session is null || string.IsNullOrEmpty(session.Id))
        {
            throw new HttpRequestException("OpenCode session create returned no session id.");
        }
        return session.Id;
    }

    private async Task SendPromptAsync(
        Uri endpoint,
        string workspacePath,
        string sessionId,
        string providerId,
        string modelId,
        string prompt,
        CancellationToken ct)
    {
        var body = new
        {
            model = new { providerID = providerId, modelID = modelId },
            parts = new[] { new { type = "text", text = prompt } },
        };
        using var content = JsonContent.Create(body);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(endpoint, $"session/{Uri.EscapeDataString(sessionId)}/prompt_async", workspacePath))
        {
            Content = content,
        };
        OpencodeServerAuth.Apply(request);
        // prompt_async replies 204 No Content once the message is accepted — success status is the
        // whole signal, there is no JSON body to read.
        using var response = await CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenCode prompt_async returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient(HttpClientName);

    private static Uri BuildUri(Uri endpoint, string path, string? workspacePath = null)
    {
        var builder = new UriBuilder(new Uri(endpoint, path));
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            builder.Query = "directory=" + Uri.EscapeDataString(workspacePath);
        }
        return builder.Uri;
    }

    private sealed record SessionResponse(string? Id);
}
