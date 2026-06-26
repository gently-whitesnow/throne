using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Repositories;

/// <summary>
/// Shared SQLite seeding for the intent-repositories integration tests (controller +
/// refresh suites). Keeps the create-intent / create-ready-binding choreography in one
/// place instead of duplicating it per test class.
/// </summary>
internal static class RepositoriesApiSeed
{
    public static async Task<Intent> IntentAsync(RepositoriesApiFixture fixture, DateTimeOffset now)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var intent = Intent.Create(IntentId.New(), "intent-for-repo-tests", [TagId.New()], now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, intent.Id.Value,
            intent.State.Text, now, TextVersionAuthor.User);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent, now), Array.Empty<Tag>(), ct),
            CancellationToken.None);
        return intent;
    }

    public static async Task<IntentRepositoryBinding> ReadyBindingAsync(
        RepositoriesApiFixture fixture, IntentId intentId, int? pullRequestNumber, DateTimeOffset now)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bindings = scope.ServiceProvider.GetRequiredService<IIntentRepositoryBindingRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var binding = IntentRepositoryBinding.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            defaultBranch: "main",
            workspacePath: Path.Combine(fixture.WorkspaceRoot, "intents", intentId.Value, "octo__hello"),
            pullRequestNumber: pullRequestNumber,
            now: now);
        // Walk pending → cloning → ready so a `clone_status=ready` precondition is satisfied
        // before the per-test scenario flips a specific surface.
        binding.MarkCloning(now);
        binding.MarkReady(now);

        await uow.ExecuteAsync(ct => bindings.CreateAsync(binding, ct), CancellationToken.None);
        return binding;
    }

    private static IntentStatusChange InitialStatusChange(Intent intent, DateTimeOffset now) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.State.CurrentVersion,
            intent.State.Status,
            intent.State.Status,
            "test:create",
            now,
            IntentTrainingAuthor.Agent);
}
