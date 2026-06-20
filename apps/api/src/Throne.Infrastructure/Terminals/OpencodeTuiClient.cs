using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Delivers the OpenCode initial prompt through the provider's HTTP server API instead of the
/// front-end TUI command bus. The server endpoints (<c>POST /session</c>,
/// <c>POST /session/{id}/prompt_async</c>) are authoritative the moment <c>/global/health</c>
/// returns 200 — they create and run the session regardless of whether the TUI front has yet
/// subscribed to its command channel. The earlier <c>tui/append-prompt</c>+<c>tui/submit-prompt</c>
/// path raced that subscription: append returned <c>true</c> while the front silently dropped the
/// text, so submit fired on an empty composer and the prompt was lost.
///
/// <c>tui/select-session</c> (step 3) is the only TUI-front call left and is best-effort: it merely
/// focuses the already-running session so the operator sees it. If it never takes, the session is
/// still live on the server, so we log and return rather than failing the spawn.
/// </summary>
internal sealed class OpencodeTuiClient(
    IHttpClientFactory httpClientFactory,
    RunPreflightOptions options,
    TimeProvider clock,
    ILogger<OpencodeTuiClient> logger) : IOpencodeTuiClient
{
    public const string HttpClientName = "opencode-tui";

    public async Task SubmitInitialPromptAsync(
        Uri endpoint,
        string workspacePath,
        string prompt,
        CancellationToken ct)
    {
        await WaitForHealthAsync(endpoint, ct);
        var sessionId = await CreateSessionAsync(endpoint, workspacePath, ct);
        await SendPromptAsync(endpoint, workspacePath, sessionId, prompt, ct);
        await SelectSessionAsync(endpoint, workspacePath, sessionId, ct);
    }

    private async Task WaitForHealthAsync(Uri endpoint, CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Max(100, options.TuiReadinessTimeoutMilliseconds));
        var poll = TimeSpan.FromMilliseconds(Math.Max(20, options.TuiReadinessPollIntervalMilliseconds));
        var deadline = clock.GetUtcNow() + timeout;
        Exception? lastFailure = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(endpoint, "global/health"));
                ApplyAuth(request);
                using var response = await CreateClient().SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                lastFailure = new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            catch (HttpRequestException ex)
            {
                lastFailure = ex;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                throw new TimeoutException(
                    $"OpenCode TUI server did not become healthy at {endpoint} within {timeout.TotalMilliseconds:0} ms.",
                    lastFailure);
            }

            await DelayWithinDeadlineAsync(deadline, poll, ct);
        }
    }

    private async Task<string> CreateSessionAsync(Uri endpoint, string workspacePath, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, BuildUri(endpoint, "session", workspacePath));
        ApplyAuth(request);
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
        string prompt,
        CancellationToken ct)
    {
        var body = new { parts = new[] { new { type = "text", text = prompt } } };
        using var content = JsonContent.Create(body);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(endpoint, $"session/{Uri.EscapeDataString(sessionId)}/prompt_async", workspacePath))
        {
            Content = content,
        };
        ApplyAuth(request);
        // prompt_async replies 204 No Content once the message is accepted — success status is the
        // whole signal, there is no JSON body to read.
        using var response = await CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenCode prompt_async returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private async Task SelectSessionAsync(
        Uri endpoint,
        string workspacePath,
        string sessionId,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Max(100, options.TuiReadinessTimeoutMilliseconds));
        var poll = TimeSpan.FromMilliseconds(Math.Max(20, options.TuiReadinessPollIntervalMilliseconds));
        var deadline = clock.GetUtcNow() + timeout;
        Exception? lastFailure = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await TrySelectSessionAsync(endpoint, workspacePath, sessionId, ct))
                {
                    return;
                }
                lastFailure = new HttpRequestException("OpenCode tui/select-session returned false.");
            }
            catch (HttpRequestException ex)
            {
                lastFailure = ex;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                // Session create + prompt_async already succeeded, so the prompt is live on the
                // server even if the TUI front never accepted focus. Best-effort: log and return
                // rather than failing the spawn — the operator can surface it via /tui/open-sessions.
                TerminalsLog.OpencodeSelectSessionNotFocused(logger, sessionId, endpoint, lastFailure);
                return;
            }

            await DelayWithinDeadlineAsync(deadline, poll, ct);
        }
    }

    private async Task<bool> TrySelectSessionAsync(
        Uri endpoint,
        string workspacePath,
        string sessionId,
        CancellationToken ct)
    {
        using var content = JsonContent.Create(new { sessionID = sessionId });
        using var request = new HttpRequestMessage(
            HttpMethod.Post, BuildUri(endpoint, "tui/select-session", workspacePath))
        {
            Content = content,
        };
        ApplyAuth(request);
        using var response = await CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenCode tui/select-session returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: ct);
    }

    private async Task DelayWithinDeadlineAsync(DateTimeOffset deadline, TimeSpan poll, CancellationToken ct)
    {
        var remaining = deadline - clock.GetUtcNow();
        var delay = remaining < poll ? remaining : poll;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, clock, ct);
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

    private static void ApplyAuth(HttpRequestMessage request)
    {
        var password = Environment.GetEnvironmentVariable("OPENCODE_SERVER_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        var username = Environment.GetEnvironmentVariable("OPENCODE_SERVER_USERNAME");
        if (string.IsNullOrEmpty(username))
        {
            username = "opencode";
        }

        var raw = Encoding.UTF8.GetBytes(username + ":" + password);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
    }

    private sealed record SessionResponse(string? Id);
}
