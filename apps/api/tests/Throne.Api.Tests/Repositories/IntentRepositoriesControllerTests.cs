using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Repositories;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class IntentRepositoriesControllerTests(MongoFixture mongo) : IAsyncLifetime, IDisposable
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

    [Fact(DisplayName = "GET /api/v1/intents/{id}/repositories возвращает 200 с пустым списком когда биндингов нет")]
    public async Task ListEmpty()
    {
        var intent = await SeedIntentAsync();

        var response = await _fixture.Client.GetAsync(new Uri($"/api/v1/intents/{intent.Id.Value}/repositories", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName = "GET /api/v1/intents/{id}/repositories возвращает 404 для несуществующего intent")]
    public async Task List_404_when_intent_missing()
    {
        var response = await _fixture.Client.GetAsync(new Uri("/api/v1/intents/missing/repositories", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/v1/intents/{id}/repositories — happy path: 201 + pending binding")]
    public async Task Bind_happy_path()
    {
        var intent = await SeedIntentAsync();
        var body = new
        {
            provider = "github",
            owner = "octo",
            repo = "hello",
            default_branch = "main",
            pull_request_number = 7,
        };

        var response = await _fixture.Client.PostAsJsonAsync(new Uri($"/api/v1/intents/{intent.Id.Value}/repositories", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("intent_id").GetString().Should().Be(intent.Id.Value);
        dto.GetProperty("owner").GetString().Should().Be("octo");
        dto.GetProperty("repo").GetString().Should().Be("hello");
        dto.GetProperty("clone_status").GetString().Should().Be("pending");
        dto.GetProperty("pull_request_number").GetInt32().Should().Be(7);
        dto.GetProperty("workspace_path").GetString().Should().Contain($"intents/{intent.Id.Value}");
    }

    [Fact(DisplayName = "POST /api/v1/intents/{id}/repositories повтор биндинга на тот же (owner, repo) даёт 409")]
    public async Task Bind_duplicate_returns_409()
    {
        var intent = await SeedIntentAsync();
        var body = new { provider = "github", owner = "octo", repo = "hello" };

        var first = await _fixture.Client.PostAsJsonAsync(new Uri($"/api/v1/intents/{intent.Id.Value}/repositories", UriKind.Relative), body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _fixture.Client.PostAsJsonAsync(new Uri($"/api/v1/intents/{intent.Id.Value}/repositories", UriKind.Relative), body);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("repository_binding.already_exists");
    }

    [Fact(DisplayName = "POST /api/v1/intents/{id}/repositories с unauthenticated провайдером даёт 422")]
    public async Task Bind_unauthenticated_returns_422()
    {
        _fixture.Provider.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderAuthStatus(GitProviderNames.GitHub, IsAuthenticated: false, Detail: "run gh auth login")));

        var intent = await SeedIntentAsync();
        var body = new { provider = "github", owner = "octo", repo = "hello" };

        var response = await _fixture.Client.PostAsJsonAsync(new Uri($"/api/v1/intents/{intent.Id.Value}/repositories", UriKind.Relative), body);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("repository.provider_not_authenticated");
    }

    [Fact(DisplayName = "DELETE /api/v1/intents/{id}/repositories/{bid} идемпотентен — 204 даже когда биндинг отсутствует")]
    public async Task Unbind_is_idempotent()
    {
        var intent = await SeedIntentAsync();

        var response = await _fixture.Client.DeleteAsync(new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/never-existed", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "POST .../sync для биндинга без PR-номера даёт 409")]
    public async Task Sync_without_pr_returns_409()
    {
        var intent = await SeedIntentAsync();
        var binding = await SeedReadyBindingAsync(intent.Id, pullRequestNumber: null);

        var response = await _fixture.Client.PostAsync(
            new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/{binding.Id.Value}/sync", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("repository_binding.pull_request_not_attached");
    }

    [Fact(DisplayName = "POST .../sync для неизвестного binding даёт 404")]
    public async Task Sync_missing_binding_returns_404()
    {
        var intent = await SeedIntentAsync();

        var response = await _fixture.Client.PostAsync(
            new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/missing-binding/sync", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET .../pull-request/comments для биндинга без PR-номера даёт 409")]
    public async Task ListComments_without_pr_returns_409()
    {
        var intent = await SeedIntentAsync();
        var binding = await SeedReadyBindingAsync(intent.Id, pullRequestNumber: null);

        var response = await _fixture.Client.GetAsync(
            new Uri($"/api/v1/intents/{intent.Id.Value}/repositories/{binding.Id.Value}/pull-request/comments", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<Intent> SeedIntentAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var intent = Intent.Create(IntentId.New(), "intent-for-repo-tests", [Throne.Domain.Tags.TagId.New()], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, intent.Id.Value,
            intent.State.Text, Now, TextVersionAuthor.User);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);
        return intent;
    }

    private async Task<IntentRepositoryBinding> SeedReadyBindingAsync(IntentId intentId, int? pullRequestNumber)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var bindings = scope.ServiceProvider.GetRequiredService<IIntentRepositoryBindingRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var binding = IntentRepositoryBinding.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            defaultBranch: "main",
            workspacePath: Path.Combine(_fixture.WorkspaceRoot, "intents", intentId.Value, "octo__hello"),
            pullRequestNumber: pullRequestNumber,
            now: Now);
        // Walk pending → cloning → ready so the sync precondition (`clone_status=ready`)
        // is satisfied before the per-test scenario flips a specific failure surface.
        binding.MarkCloning(Now);
        binding.MarkReady(Now);

        await uow.ExecuteAsync(ct => bindings.CreateAsync(binding, ct), CancellationToken.None);
        return binding;
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.State.CurrentVersion,
            intent.State.Status,
            intent.State.Status,
            "test:create",
            Now,
            IntentTrainingAuthor.Agent);
}
