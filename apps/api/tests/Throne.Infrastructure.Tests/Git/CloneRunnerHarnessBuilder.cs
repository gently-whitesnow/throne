using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// DI-обвязка для harness'a: собирает реальные канал/workflow/recovery поверх
/// in-memory binding-store и фейкового провайдера, цепляет <see cref="CloneInFlightTracker"/>
/// в stub-метод <c>CloneRepositoryAsync</c>.
/// </summary>
internal static class CloneRunnerHarnessBuilder
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    public static CloneRunnerStand Build(int maxParallel, CloneInFlightTracker tracker)
    {
        var channel = new RepositoryCloneRequestsChannel();
        var bindingsRepo = new InMemoryIntentRepositoryBindingRepository();
        var provider = BuildProvider(tracker);
        var registry = Substitute.For<IGitProviderRegistry>();
        registry.GetByName(GitProviderNames.GitHub).Returns(provider);

        var sp = BuildServiceProvider(channel, bindingsRepo, registry, maxParallel);
        var service = new RepositoryCloneService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            channel,
            sp.GetRequiredService<IOptions<CloneRunnerOptions>>(),
            NullLogger<RepositoryCloneService>.Instance);

        return new CloneRunnerStand(channel, bindingsRepo, service);
    }

    public static IReadOnlyList<IntentRepositoryBinding> MakePendingBindings(int count) =>
        Enumerable.Range(0, count).Select(i => NewPendingBinding($"intent-{i}")).ToArray();

    private static IGitProvider BuildProvider(CloneInFlightTracker tracker)
    {
        var provider = Substitute.For<IGitProvider>();
        provider.ProviderName.Returns(GitProviderNames.GitHub);
        provider.CloneRepositoryAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => tracker.BeginAsync());
        return provider;
    }

    private static IServiceProvider BuildServiceProvider(
        RepositoryCloneRequestsChannel channel,
        InMemoryIntentRepositoryBindingRepository bindingsRepo,
        IGitProviderRegistry registry,
        int maxParallel)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedClock(Now));
        services.AddSingleton<IIntentRepositoryBindingRepository>(bindingsRepo);
        services.AddSingleton(registry);
        services.AddSingleton<IUnitOfWork>(new PassthroughUnitOfWork());
        services.AddSingleton<IRepositoryCloneRequests>(channel);
        services.AddSingleton<IRepositoryCloneRequestsReader>(channel);
        services.AddSingleton<RepositoryCloneTransitionWriter>();
        var workspace = Substitute.For<IWorkspaceRootProvider>();
        workspace.ResolvedRoot.Returns("/tmp/throne-test-workspaces");
        services.AddSingleton(workspace);
        services.AddSingleton<RepositoryCloneWorkflow>();
        services.AddSingleton<RepositoryCloneRecoveryWorkflow>();
        services.AddSingleton(Options.Create(new CloneRunnerOptions { MaxParallel = maxParallel }));
        return services.BuildServiceProvider();
    }

    private static IntentRepositoryBinding NewPendingBinding(string intentId)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(intentId),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", $"repo-{intentId}"),
            WorkspacePath: $"/tmp/throne-test-workspaces/intents/{intentId}/octo__repo",
            DefaultBranch: "main",
            CloneStatus: CloneStatusNames.Pending,
            CloneError: null,
            PullRequestNumber: null,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBinding.Restore(snapshot);
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal sealed record CloneRunnerStand(
    RepositoryCloneRequestsChannel Channel,
    InMemoryIntentRepositoryBindingRepository Bindings,
    RepositoryCloneService Service);
