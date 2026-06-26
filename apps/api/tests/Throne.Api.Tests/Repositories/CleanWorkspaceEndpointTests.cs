using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Repositories;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Repositories;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class CleanWorkspaceEndpointTests(SqliteFixture sqlite) : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri CleanUri = new("/api/v1/settings/workspace/clean", UriKind.Relative);

    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(sqlite, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "POST .../workspace/clean dry_run=true (closed_only) считает только закрытые и ничего не удаляет")]
    public async Task DryRun_closed_only_counts_without_deleting()
    {
        var closed = await SeedIntentAsync(IntentStatusNames.Done);
        var active = await SeedIntentAsync(IntentStatusNames.Work);
        var closedPath = await SeedBindingWithFolderAsync(closed.Id, "octo", "closed", fileBytes: 100);
        var activePath = await SeedBindingWithFolderAsync(active.Id, "octo", "active", fileBytes: 200);

        var response = await _fixture.Client.PostAsJsonAsync(
            CleanUri, new { mode = "closed_only", dry_run = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("removed_clones").GetInt32().Should().Be(1);
        dto.GetProperty("freed_bytes").GetInt64().Should().Be(100);
        dto.GetProperty("dry_run").GetBoolean().Should().BeTrue();

        Directory.Exists(closedPath).Should().BeTrue("dry-run не трогает диск");
        Directory.Exists(activePath).Should().BeTrue();
        (await CountAllBindingsAsync()).Should().Be(2);
    }

    [Fact(DisplayName = "POST .../workspace/clean (closed_only) удаляет клон+биндинг закрытого интента, активный не трогает")]
    public async Task Closed_only_removes_closed_keeps_active()
    {
        var closed = await SeedIntentAsync(IntentStatusNames.Reject);
        var active = await SeedIntentAsync(IntentStatusNames.Work);
        var closedPath = await SeedBindingWithFolderAsync(closed.Id, "octo", "closed", fileBytes: 100);
        var activePath = await SeedBindingWithFolderAsync(active.Id, "octo", "active", fileBytes: 200);

        var response = await _fixture.Client.PostAsJsonAsync(
            CleanUri, new { mode = "closed_only", dry_run = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("removed_clones").GetInt32().Should().Be(1);
        dto.GetProperty("freed_bytes").GetInt64().Should().Be(100);
        dto.GetProperty("dry_run").GetBoolean().Should().BeFalse();

        Directory.Exists(closedPath).Should().BeFalse();
        Directory.Exists(activePath).Should().BeTrue();
        (await CountBindingsForAsync(closed.Id)).Should().Be(0, "папка удалена ⇒ запись удалена");
        (await CountBindingsForAsync(active.Id)).Should().Be(1);
    }

    [Fact(DisplayName = "POST .../workspace/clean (all) сносит весь корень и удаляет все биндинги")]
    public async Task All_clears_root_and_drops_every_binding()
    {
        var first = await SeedIntentAsync(IntentStatusNames.Work);
        var second = await SeedIntentAsync(IntentStatusNames.Done);
        await SeedBindingWithFolderAsync(first.Id, "octo", "one", fileBytes: 100);
        await SeedBindingWithFolderAsync(second.Id, "octo", "two", fileBytes: 200);

        var response = await _fixture.Client.PostAsJsonAsync(
            CleanUri, new { mode = "all", dry_run = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("removed_clones").GetInt32().Should().Be(2);
        dto.GetProperty("freed_bytes").GetInt64().Should().Be(300);

        Directory.EnumerateFileSystemEntries(_fixture.WorkspaceRoot).Should().BeEmpty();
        (await CountAllBindingsAsync()).Should().Be(0);
    }

    private async Task<int> CountAllBindingsAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var bindings = scope.ServiceProvider.GetRequiredService<IIntentRepositoryBindingRepository>();
        return (await bindings.FindAllAsync(CancellationToken.None)).Count;
    }

    private async Task<int> CountBindingsForAsync(IntentId intentId)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var bindings = scope.ServiceProvider.GetRequiredService<IIntentRepositoryBindingRepository>();
        return (await bindings.FindByIntentAsync(intentId, CancellationToken.None)).Count;
    }

    private async Task<string> SeedBindingWithFolderAsync(
        IntentId intentId, string owner, string repo, long fileBytes)
    {
        var path = Path.Combine(_fixture.WorkspaceRoot, "intents", intentId.Value, $"{owner}__{repo}");
        Directory.CreateDirectory(path);
        await File.WriteAllBytesAsync(Path.Combine(path, "blob.bin"), new byte[fileBytes]);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var bindings = scope.ServiceProvider.GetRequiredService<IIntentRepositoryBindingRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var binding = IntentRepositoryBinding.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            defaultBranch: "main",
            workspacePath: path,
            pullRequestNumber: null,
            now: Now);
        binding.MarkCloning(Now);
        binding.MarkReady(Now);
        await uow.ExecuteAsync(ct => bindings.CreateAsync(binding, ct), CancellationToken.None);
        return path;
    }

    private async Task<Intent> SeedIntentAsync(string status)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var intent = Intent.Create(IntentId.New(), "intent-for-clean-tests", [Throne.Domain.Tags.TagId.New()], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, intent.Id.Value,
            intent.State.Text, Now, TextVersionAuthor.User);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);

        if (!string.Equals(intent.State.Status, status, StringComparison.Ordinal))
        {
            await uow.ExecuteAsync(
                ct => repo.SetStatusAsync(
                    intent.Id, status, appendText: null, reason: null,
                    IntentTrainingAuthor.Agent, "test:seed", Now, ct),
                CancellationToken.None);
        }
        return intent;
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
