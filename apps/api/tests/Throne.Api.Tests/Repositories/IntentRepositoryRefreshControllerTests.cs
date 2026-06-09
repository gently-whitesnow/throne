using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Repositories;

/// <summary>
/// HTTP-level coverage for <c>POST .../repositories/{binding_id}/refresh</c> (ADR-0024
/// «Обновить» disk-recovery). Exercises the real on-disk probe + live path recompute +
/// Mongo transition: folder missing → re-queued to <c>pending</c>, folder present → no-op.
/// </summary>
[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class IntentRepositoryRefreshControllerTests(MongoFixture mongo) : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(mongo, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "POST .../refresh: папки нет на диске → 200 + binding снова pending")]
    public async Task Refresh_missing_folder_requeues_pending()
    {
        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var binding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, intent.Id, pullRequestNumber: null, Now);

        var response = await _fixture.Client.PostAsync(
            new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/{binding.Id.Value}/refresh", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("id").GetString().Should().Be(binding.Id.Value);
        dto.GetProperty("clone_status").GetString().Should().Be("pending");
    }

    [Fact(DisplayName = "POST .../refresh: папка есть на диске → 200 + binding без изменений (no-op)")]
    public async Task Refresh_existing_folder_is_noop()
    {
        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var binding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, intent.Id, pullRequestNumber: null, Now);
        Directory.CreateDirectory(
            Path.Combine(_fixture.WorkspaceRoot, "intents", intent.Id.Value, "octo__hello"));

        var response = await _fixture.Client.PostAsync(
            new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/{binding.Id.Value}/refresh", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("clone_status").GetString().Should().Be("ready");
    }

    [Fact(DisplayName = "POST .../refresh для неизвестного binding даёт 404")]
    public async Task Refresh_missing_binding_returns_404()
    {
        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);

        var response = await _fixture.Client.PostAsync(
            new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/missing-binding/refresh", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
