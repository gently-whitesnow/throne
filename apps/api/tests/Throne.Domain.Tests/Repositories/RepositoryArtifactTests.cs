using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class RepositoryArtifactTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoCoordinate Coordinate = new(GitProviderNames.GitHub, "octo", "throne");

    private static RepositoryArtifact NewArtifact(
        string slug = "db-schema-map",
        string title = "DB schema map",
        string document = "# erd") =>
        RepositoryArtifact.Create(RepositoryArtifactId.New(), Coordinate, slug, title, document, Now);

    [Fact(DisplayName = "Create стартует с version=1 и одинаковыми временными метками")]
    public void Create_starts_at_version_one()
    {
        var artifact = NewArtifact();

        artifact.Version.Should().Be(1);
        artifact.Slug.Should().Be("db-schema-map");
        artifact.CreatedAt.Should().Be(Now);
        artifact.UpdatedAt.Should().Be(Now);
    }

    [Theory(DisplayName = "Create отвергает невалидный slug")]
    [InlineData("Db-Schema")]
    [InlineData("db schema")]
    [InlineData("db--schema")]
    [InlineData("-schema")]
    [InlineData("")]
    public void Create_rejects_invalid_slug(string slug)
    {
        var act = () => NewArtifact(slug: slug);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Create отвергает пустой title")]
    public void Create_rejects_blank_title()
    {
        var act = () => NewArtifact(title: "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Update заменяет поля и инкрементит version")]
    public void Update_bumps_version()
    {
        var artifact = NewArtifact();
        var later = Now.AddMinutes(10);

        artifact.Update("New title", "## changed", later);

        artifact.Version.Should().Be(2);
        artifact.Title.Should().Be("New title");
        artifact.Document.Should().Be("## changed");
        artifact.UpdatedAt.Should().Be(later);
        artifact.CreatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "Restore перепроверяет инварианты на version < 1")]
    public void Restore_rejects_non_positive_version()
    {
        var snapshot = new RepositoryArtifactSnapshot(
            RepositoryArtifactId.New(), Coordinate, "db-schema-map", "Title", "body",
            Version: 0, Now, Now);

        var act = () => RepositoryArtifact.Restore(snapshot);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Capture снимает текущее состояние артефакта в version-строку")]
    public void Capture_snapshots_current_state()
    {
        var artifact = NewArtifact();
        artifact.Update("v2 title", "v2 body", Now.AddMinutes(1));

        var version = RepositoryArtifactVersion.Capture(artifact);

        version.ArtifactId.Should().Be(artifact.Id);
        version.Version.Should().Be(2);
        version.Title.Should().Be("v2 title");
        version.Document.Should().Be("v2 body");
    }
}
