using System.Net.Http.Headers;
using System.Text.Json;

namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal sealed class GenericHttpClient(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "custom-http-task-tracker";

    public Task ProbeAsync(GenericHttpConnection connection, CancellationToken ct) =>
        SendAsync(connection, "/health", ct);

    public async Task<IReadOnlyList<GenericHttpBoardDto>> ListBoardsAsync(
        GenericHttpConnection connection,
        CancellationToken ct) =>
        (await GetAsync<GenericHttpBoardsResponse>(connection, "/boards", ct)).Boards;

    public async Task<IReadOnlyList<GenericHttpCardDto>> ListCardsAsync(
        GenericHttpConnection connection,
        string boardId,
        CancellationToken ct) =>
        (await GetAsync<GenericHttpCardsResponse>(
            connection, $"/boards/{Uri.EscapeDataString(boardId)}/cards", ct)).Cards;

    public async Task<IReadOnlyList<GenericHttpCardDto>> SearchCardsAsync(
        GenericHttpConnection connection,
        string boardId,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var path = $"/boards/{Uri.EscapeDataString(boardId)}/cards/search"
            + $"?query={Uri.EscapeDataString(query ?? string.Empty)}&limit={limit}";
        return (await GetAsync<GenericHttpCardsResponse>(connection, path, ct)).Cards;
    }

    public Task<GenericHttpCardDto> GetCardAsync(
        GenericHttpConnection connection,
        string cardId,
        CancellationToken ct) =>
        GetAsync<GenericHttpCardDto>(
            connection,
            $"/cards/{Uri.EscapeDataString(cardId)}",
            ct);

    private async Task<T> GetAsync<T>(GenericHttpConnection connection, string path, CancellationToken ct) =>
        JsonSerializer.Deserialize<T>(await SendAsync(connection, path, ct), GenericHttpJson.Options)
        ?? throw new InvalidOperationException("Generic task-tracker API returned an empty body.");

    private async Task<string> SendAsync(GenericHttpConnection connection, string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.BaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Token);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, connection.ApiBaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync(ct);
        }

        throw new GenericHttpApiException(
            response.StatusCode,
            await response.Content.ReadAsStringAsync(ct));
    }
}
