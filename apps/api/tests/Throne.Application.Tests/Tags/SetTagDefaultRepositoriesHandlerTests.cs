using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Tags;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Application.Tests.Tags;

public class SetTagDefaultRepositoriesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 11, 0, 0, TimeSpan.Zero);
    private static readonly TagId TagIdValue = TagId.New();

    [Fact(DisplayName = "Updated outcome возвращает обновлённый Tag")]
    public async Task Updated_outcome_returns_tag()
    {
        var fixture = new Fixture();
        var refreshed = NewTag(TagIdValue, currentVersion: 2);
        fixture.OutcomeIs(new SetTagDefaultRepositoriesOutcome.Updated(refreshed));

        var tag = await fixture.Handler.HandleAsync(
            new SetTagDefaultRepositoriesCommand(TagIdValue.Value, 1, [NewDefault()]),
            CancellationToken.None);

        tag.Should().Be(refreshed);
    }

    [Fact(DisplayName = "NoChange outcome тоже возвращает Tag без эксепшна")]
    public async Task NoChange_outcome_returns_tag()
    {
        var fixture = new Fixture();
        var unchanged = NewTag(TagIdValue, currentVersion: 1);
        fixture.OutcomeIs(new SetTagDefaultRepositoriesOutcome.NoChange(unchanged));

        var tag = await fixture.Handler.HandleAsync(
            new SetTagDefaultRepositoriesCommand(TagIdValue.Value, 1, [NewDefault()]),
            CancellationToken.None);

        tag.Should().Be(unchanged);
    }

    [Fact(DisplayName = "NotFound → tag.not_found")]
    public async Task NotFound_outcome_throws()
    {
        var fixture = new Fixture();
        fixture.OutcomeIs(new SetTagDefaultRepositoriesOutcome.NotFound());

        var act = () => fixture.Handler.HandleAsync(
            new SetTagDefaultRepositoriesCommand(TagIdValue.Value, 1, [NewDefault()]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TagNotFound);
    }

    [Fact(DisplayName = "VersionConflict → tag.version_conflict с current_version в payload")]
    public async Task VersionConflict_outcome_throws()
    {
        var fixture = new Fixture();
        fixture.OutcomeIs(new SetTagDefaultRepositoriesOutcome.VersionConflict(CurrentVersion: 5));

        var act = () => fixture.Handler.HandleAsync(
            new SetTagDefaultRepositoriesCommand(TagIdValue.Value, 1, [NewDefault()]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TagVersionConflict);
        ex.Which.Extensions["current_version"].Should().Be(5);
    }

    [Fact(DisplayName = "Handle материализует Repository-реестр по каждой координате списка")]
    public async Task Handle_ensures_registry_per_coordinate()
    {
        var fixture = new Fixture();
        fixture.OutcomeIs(new SetTagDefaultRepositoriesOutcome.Updated(NewTag(TagIdValue, currentVersion: 2)));
        var alpha = new RepoCoordinate(GitProviderNames.GitHub, "octo", "alpha");
        var beta = new RepoCoordinate(GitProviderNames.GitHub, "octo", "beta");

        await fixture.Handler.HandleAsync(
            new SetTagDefaultRepositoriesCommand(
                TagIdValue.Value,
                1,
                [new TagDefaultRepository(alpha, "main"), new TagDefaultRepository(beta, null)]),
            CancellationToken.None);

        await fixture.Registry.Received(1).EnsureRepositoryAsync(alpha, Now, Arg.Any<CancellationToken>());
        await fixture.Registry.Received(1).EnsureRepositoryAsync(beta, Now, Arg.Any<CancellationToken>());
    }

    private static TagDefaultRepository NewDefault() =>
        new(new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"), DefaultBranch: "main");

    private static Tag NewTag(TagId id, int currentVersion) =>
        Tag.Restore(id, "review", currentVersion, Now, Now);

    private sealed class Fixture
    {
        public Fixture()
        {
            Repository = Substitute.For<ITagRepository>();
            Registry = Substitute.For<IRepositoryRegistry>();
            Handler = new SetTagDefaultRepositoriesHandler(
                Repository, Registry, new PassthroughUnitOfWork(), new FixedClock(Now));
        }

        public ITagRepository Repository { get; }
        public IRepositoryRegistry Registry { get; }
        public SetTagDefaultRepositoriesHandler Handler { get; }

        public void OutcomeIs(SetTagDefaultRepositoriesOutcome outcome) =>
            Repository.SetDefaultRepositoriesAsync(
                    Arg.Any<TagId>(),
                    Arg.Any<int>(),
                    Arg.Any<IReadOnlyList<TagDefaultRepository>>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<CancellationToken>())
                .Returns(outcome);
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
