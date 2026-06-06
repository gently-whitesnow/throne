using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Mcp.Tools;
using Throne.Api.Tests.Infrastructure;
using Throne.Api.Tests.Repositories;
using Throne.Application.Errors;
using Throne.Domain.Repositories;

namespace Throne.Api.Tests.Mcp;

/// <summary>
/// MCP-contract round-trip for the repository knowledge-page tools (ADR-0031): the tools are
/// resolved from the real DI container and exercised against a Testcontainers Mongo, so the
/// write/get/list surface is verified end-to-end through <see cref="RepositoryDocumentTools"/>
/// + <c>RepositoryArtifactWriter</c> + the Mongo stores, not against substitutes.
/// </summary>
[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class RepositoryDocumentToolsTests(MongoFixture mongo) : IAsyncLifetime, IDisposable
{
    private const string Provider = GitProviderNames.GitHub;
    private const string Owner = "octo";
    private const string Repo = "throne";

    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(mongo, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    private RepositoryDocumentTools Tools => _fixture.Services.GetRequiredService<RepositoryDocumentTools>();

    [Fact(DisplayName = "write→get round-trip: страница db-schema-map возвращается с render_hint=schema_map и version=1")]
    public async Task Write_then_get_round_trips_schema_map()
    {
        var written = await Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug,
            "DB schema", "# erDiagram", expected_version: null, CancellationToken.None);

        written.Version.Should().Be(1);
        written.RenderHint.Should().Be(RepositoryArtifactRenderHints.SchemaMap);

        var fetched = await Tools.GetRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug, CancellationToken.None);

        fetched.Title.Should().Be("DB schema");
        fetched.Document.Should().Be("# erDiagram");
        fetched.RenderHint.Should().Be(RepositoryArtifactRenderHints.SchemaMap);
        fetched.Version.Should().Be(1);
    }

    [Fact(DisplayName = "render_hint выводится из slug: произвольный slug → markdown")]
    public async Task Non_schema_slug_renders_as_markdown()
    {
        var written = await Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, "architecture-overview",
            "Arch", "body", expected_version: null, CancellationToken.None);

        written.RenderHint.Should().Be(RepositoryArtifactRenderHints.Markdown);
    }

    [Fact(DisplayName = "update с current expected_version бампит версию")]
    public async Task Update_with_matching_version_bumps()
    {
        await Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug,
            "v1", "b1", expected_version: null, CancellationToken.None);

        var updated = await Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug,
            "v2", "b2", expected_version: 1, CancellationToken.None);

        updated.Version.Should().Be(2);
    }

    [Fact(DisplayName = "write со stale expected_version бросает typed ApiException version_conflict (→ MCP error)")]
    public async Task Stale_expected_version_throws_typed_conflict()
    {
        await Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug,
            "v1", "b1", expected_version: null, CancellationToken.None);

        var act = () => Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug,
            "v2", "b2", expected_version: 5, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryArtifactVersionConflict);
        ex.Which.Extensions["current_version"].Should().Be(1);
    }

    [Fact(DisplayName = "get несуществующей страницы бросает typed ApiException not_found (→ MCP error)")]
    public async Task Get_missing_page_throws_typed_not_found()
    {
        var act = () => Tools.GetRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryArtifactNotFound);
    }

    [Fact(DisplayName = "list_repositories отдаёт координату, зарегистрированную записью страницы, и её slug'и")]
    public async Task List_repositories_returns_registered_with_slugs()
    {
        await Tools.WriteRepositoryDocument(
            Provider, Owner, Repo, RepositoryArtifactRenderHints.SchemaMapSlug,
            "map", "body", expected_version: null, CancellationToken.None);

        var result = await Tools.ListRepositories(CancellationToken.None);

        var entry = result.Items.Should().ContainSingle(
            r => r.Provider == Provider && r.Owner == Owner && r.Repo == Repo).Subject;
        entry.DocumentSlugs.Should().Contain(RepositoryArtifactRenderHints.SchemaMapSlug);
    }

    [Fact(DisplayName = "list_repositories пуст, пока ничего не зарегистрировано")]
    public async Task List_repositories_empty_when_nothing_registered()
    {
        var result = await Tools.ListRepositories(CancellationToken.None);

        result.Items.Should().BeEmpty();
    }
}
