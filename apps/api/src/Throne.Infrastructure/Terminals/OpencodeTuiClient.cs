using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal sealed class OpencodeTuiClient(
    IHttpClientFactory httpClientFactory,
    RunPreflightOptions options,
    TimeProvider clock) : IOpencodeTuiClient
{
    public const string HttpClientName = "opencode-tui";

    public async Task SubmitInitialPromptAsync(
        Uri endpoint,
        string workspacePath,
        string prompt,
        CancellationToken ct)
    {
        await WaitForHealthAsync(endpoint, ct);
        await PostJsonAsync(endpoint, "tui/append-prompt", workspacePath, new { text = prompt }, ct);
        await PostAsync(endpoint, "tui/submit-prompt", workspacePath, content: null, ct);
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

            var remaining = deadline - clock.GetUtcNow();
            var delay = remaining < poll ? remaining : poll;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, clock, ct);
            }
        }
    }

    private async Task PostJsonAsync(
        Uri endpoint,
        string path,
        string workspacePath,
        object body,
        CancellationToken ct)
    {
        using var content = JsonContent.Create(body);
        await PostAsync(endpoint, path, workspacePath, content, ct);
    }

    private async Task PostAsync(
        Uri endpoint,
        string path,
        string workspacePath,
        HttpContent? content,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(endpoint, path, workspacePath))
        {
            Content = content,
        };
        ApplyAuth(request);
        using var response = await CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenCode TUI {path} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var result = await response.Content.ReadFromJsonAsync<bool>(cancellationToken: ct);
        if (result != true)
        {
            throw new HttpRequestException($"OpenCode TUI {path} returned false.");
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
}
