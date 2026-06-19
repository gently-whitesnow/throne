using FluentAssertions;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Гварды на доменные события, переносимые carrier-результатами реестра/страниц (ADR-0031):
/// событие фанаутится ровно один раз и только на содержательном исходе
/// (Created / Written), а на идемпотентном (Existed) и конфликте (VersionConflict) — молчит.
/// </summary>
public class RepositoryRegistryOutcomeEventsTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoCoordinate Coordinate = new(GitProviderNames.GitHub, "octo", "throne");

    [Fact(DisplayName = "EnsureRepositoryOutcome.Created несёт ровно один RepositoryRegistered")]
    public void Created_carries_single_registered_event()
    {
        var repository = Repository.Create(RepositoryId.New(), Coordinate, Now);

        var outcome = new EnsureRepositoryOutcome.Created(repository);

        outcome.Events.Should().ContainSingle()
            .Which.Should().BeOfType<RepositoryRegistered>()
            .Which.Repository.Should().BeSameAs(repository);
    }

    [Fact(DisplayName = "EnsureRepositoryOutcome.Existed не несёт событий (идемпотентный no-op)")]
    public void Existed_carries_no_events()
    {
        var repository = Repository.Create(RepositoryId.New(), Coordinate, Now);

        var outcome = new EnsureRepositoryOutcome.Existed(repository);

        outcome.Events.Should().BeEmpty();
    }

    [Fact(DisplayName = "WriteRepositoryArtifactOutcome.Written несёт ровно один RepositoryDocumentUpdated")]
    public void Written_carries_single_document_updated_event()
    {
        var artifact = RepositoryArtifact.Create(
            RepositoryArtifactId.New(), Coordinate, "db-schema-map", "DB schema", "# erd", Now);

        var outcome = new WriteRepositoryArtifactOutcome.Written(artifact, Created: true);

        outcome.Events.Should().ContainSingle()
            .Which.Should().BeOfType<RepositoryDocumentUpdated>()
            .Which.Artifact.Should().BeSameAs(artifact);
    }

    [Fact(DisplayName = "WriteRepositoryArtifactOutcome.VersionConflict не несёт событий")]
    public void Version_conflict_carries_no_events()
    {
        var outcome = new WriteRepositoryArtifactOutcome.VersionConflict(CurrentVersion: 3);

        outcome.Events.Should().BeEmpty();
    }
}
