using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class RepositoryArtifactWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoCoordinate Coordinate = new(GitProviderNames.GitHub, "octo", "throne");

    private static WriteRepositoryArtifactCommand Command(int? expectedVersion = null) =>
        new(Coordinate, "db-schema-map", "DB schema", "# erd", expectedVersion);

    [Fact(DisplayName = "WriteAsync материализует Repository через реестр и возвращает записанный артефакт")]
    public async Task Write_ensures_registry_and_returns_artifact()
    {
        var fixture = new Fixture();
        var artifact = RepositoryArtifact.Create(
            RepositoryArtifactId.New(), Coordinate, "db-schema-map", "DB schema", "# erd", Now);
        fixture.Artifacts
            .WriteAsync(Coordinate, "db-schema-map", "DB schema", "# erd",
                null, Now, Arg.Any<CancellationToken>())
            .Returns(new WriteRepositoryArtifactOutcome.Written(artifact, Created: true));

        var result = await fixture.Writer.WriteAsync(Command(), CancellationToken.None);

        result.Should().BeSameAs(artifact);
        await fixture.Registry.Received(1).EnsureRepositoryAsync(Coordinate, Now, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "VersionConflict от стора превращается в ApiException repository_artifact.version_conflict")]
    public async Task Write_conflict_maps_to_api_exception()
    {
        var fixture = new Fixture();
        fixture.Artifacts
            .WriteAsync(Coordinate, "db-schema-map", "DB schema", "# erd",
                1, Now, Arg.Any<CancellationToken>())
            .Returns(new WriteRepositoryArtifactOutcome.VersionConflict(3));

        var act = () => fixture.Writer.WriteAsync(Command(expectedVersion: 1), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryArtifactVersionConflict);
        ex.Which.Extensions["current_version"].Should().Be(3);
        await fixture.Registry.DidNotReceive()
            .EnsureRepositoryAsync(Arg.Any<RepoCoordinate>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Artifacts = Substitute.For<IRepositoryArtifactRepository>();
            Registry = Substitute.For<IRepositoryRegistry>();
            // Registry must return a non-null outcome: the writer aggregates its Events.
            Registry
                .EnsureRepositoryAsync(Coordinate, Now, Arg.Any<CancellationToken>())
                .Returns(new EnsureRepositoryOutcome.Existed(
                    Repository.Create(RepositoryId.New(), Coordinate, Now)));
            Writer = new RepositoryArtifactWriter(
                Artifacts, Registry, new PassthroughUnitOfWork(), new FixedClock(Now));
        }

        public IRepositoryArtifactRepository Artifacts { get; }
        public IRepositoryRegistry Registry { get; }
        public RepositoryArtifactWriter Writer { get; }
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
