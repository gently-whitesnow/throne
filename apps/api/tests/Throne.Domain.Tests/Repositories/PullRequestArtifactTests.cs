using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class PullRequestArtifactTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly BindingId BindingId = new("binding-1");

    [Fact(DisplayName = "Create фиксирует one-shot поля PR-артефакта")]
    public void Create_sets_fields()
    {
        var artifact = NewArtifact();

        artifact.BindingId.Should().Be(BindingId);
        artifact.PullRequestNumber.Should().Be(42);
        artifact.Type.Should().Be("static_analysis");
        artifact.Render.Should().Be(PullRequestArtifactRenderNames.Markdown);
        artifact.Source.Should().Be(PullRequestArtifactSourceNames.Static);
        artifact.ProducedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "Replace перезаписывает mutable-поля без версии")]
    public void Replace_overwrites_latest()
    {
        var artifact = NewArtifact();
        var later = Now.AddMinutes(5);

        artifact.Replace(
            42,
            PullRequestArtifactRenderNames.Json,
            "{\"ok\":true}",
            "Updated",
            PullRequestArtifactSourceNames.Agent,
            ["sha:def"],
            later);

        artifact.Render.Should().Be(PullRequestArtifactRenderNames.Json);
        artifact.Content.Should().Be("{\"ok\":true}");
        artifact.Summary.Should().Be("Updated");
        artifact.Source.Should().Be(PullRequestArtifactSourceNames.Agent);
        artifact.SourceRefs.Should().Equal("sha:def");
        artifact.ProducedAt.Should().Be(later);
    }

    [Theory(DisplayName = "Create отвергает невалидный type")]
    [InlineData("")]
    [InlineData("StaticAnalysis")]
    [InlineData("static analysis")]
    [InlineData("_static")]
    public void Create_rejects_invalid_type(string type)
    {
        var act = () => NewArtifact(type);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Create отвергает неизвестный render")]
    public void Create_rejects_unknown_render()
    {
        var act = () => PullRequestArtifact.Create(
            PullRequestArtifactId.New(),
            BindingId,
            42,
            "coverage",
            "pdf",
            "body",
            "Coverage",
            PullRequestArtifactSourceNames.Static,
            [],
            Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static PullRequestArtifact NewArtifact(string type = "static_analysis") =>
        PullRequestArtifact.Create(
            PullRequestArtifactId.New(),
            BindingId,
            42,
            type,
            PullRequestArtifactRenderNames.Markdown,
            "# body",
            "Static analysis",
            PullRequestArtifactSourceNames.Static,
            ["sha:abc"],
            Now);
}
