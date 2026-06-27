using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Throne.Infrastructure.TaskTrackers.Kaiten.Http;

/// <summary>
/// The transport at the heart of the Kaiten adapter: builds the authorized JSON request, paces it
/// through the <see cref="KaitenRateLimiter"/>, and retries per <see cref="KaitenRetryPolicy"/>. The
/// typed endpoint groups (cards, comments, …) sit on top and only supply a path, verb and payload.
/// Uses <see cref="IHttpClientFactory"/> so handler lifetime is pooled even though this is a
/// singleton; the bearer token rides on each request rather than on the shared client.
/// </summary>
internal sealed class KaitenHttpExecutor(
    IHttpClientFactory httpClientFactory,
    KaitenRateLimiter rateLimiter,
    KaitenRetryPolicy retryPolicy,
    KaitenOptions options,
    TimeProvider clock)
{
    public const string HttpClientName = "kaiten";

    public async Task<T> GetAsync<T>(KaitenConnection connection, string path, CancellationToken ct) =>
        Deserialize<T>(await SendAsync(connection, HttpMethod.Get, path, body: null, ct));

    public async Task<T> PostAsync<T>(KaitenConnection connection, string path, object body, CancellationToken ct) =>
        Deserialize<T>(await SendAsync(connection, HttpMethod.Post, path, body, ct));

    public async Task<T> PatchAsync<T>(KaitenConnection connection, string path, object body, CancellationToken ct) =>
        Deserialize<T>(await SendAsync(connection, HttpMethod.Patch, path, body, ct));

    public async Task DeleteAsync(KaitenConnection connection, string path, CancellationToken ct) =>
        await SendAsync(connection, HttpMethod.Delete, path, body: null, ct);

    private async Task<string> SendAsync(
        KaitenConnection connection, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.BaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Token);

        var client = httpClientFactory.CreateClient(HttpClientName);
        var maxAttempts = Math.Max(1, options.MaxAttempts);
        for (var attempt = 1; ; attempt++)
        {
            await rateLimiter.WaitAsync(ct);
            using var request = BuildRequest(connection, method, path, body);
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(ct);
            }

            if (!KaitenRetryPolicy.IsRetryable(response.StatusCode) || attempt >= maxAttempts)
            {
                throw new KaitenApiException(response.StatusCode, await response.Content.ReadAsStringAsync(ct));
            }

            var delay = retryPolicy.NextDelay(attempt, response, clock.GetUtcNow());
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, clock, ct);
            }
        }
    }

    private static HttpRequestMessage BuildRequest(
        KaitenConnection connection, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, connection.ApiBaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, KaitenJson.Options);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, KaitenJson.Options)
        ?? throw new InvalidOperationException("Kaiten returned an empty body where a value was expected.");
}
