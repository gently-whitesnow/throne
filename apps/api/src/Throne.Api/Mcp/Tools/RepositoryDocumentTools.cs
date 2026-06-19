using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// MCP surface for repository knowledge pages (ADR-0031). The single write exception to the
/// otherwise read-only MCP surface (ADR-0030): <c>write_repository_document</c> authors a
/// titled markdown page anchored to a <c>(provider, owner, repo)</c> coordinate, paired with
/// <c>get_repository_document</c> (read-back of the page + its <c>version</c>) and
/// <c>list_repositories</c> (known coordinates the agent matches against its clone's
/// <c>git remote</c>). Pages never enter <c>get_intent</c> or the agent's intent context.
/// </summary>
[McpServerToolType]
public sealed class RepositoryDocumentTools(
    RepositoryArtifactWriter writer,
    GetRepositoryDocumentHandler getDocument,
    ListRepositoriesHandler listRepositories)
{
    [McpServerTool(Name = "write_repository_document", UseStructuredContent = true)]
    [Description("Create or update a titled markdown knowledge page anchored to a repository (provider/owner/repo). The only write tool on this MCP surface. The page slug is unique per repository; pass `expected_version` equal to the current version (from get_repository_document) to update — omit it (or 0) to create the first version. On a version mismatch the call fails with repository_artifact.version_conflict carrying current_version; re-read and retry. Pages are deliberately invisible to get_intent and are never fed into an agent's intent context.")]
    public async Task<McpRepositoryDocumentRef> WriteRepositoryDocument(
        [Description("Git provider of the target repository (e.g. github).")] string provider,
        [Description("Repository owner / organization (e.g. gently-whitesnow).")] string owner,
        [Description("Repository name (e.g. throne).")] string repo,
        [Description("Stable kebab-case page slug, unique per repository.")] string slug,
        [Description("Human-readable page title (<=200 chars).")] string title,
        [Description("Full markdown body of the page. Replaces the previous body wholesale on update.")] string document,
        [Description("Current version to update on top of; omit or 0 to create version 1. Mismatch => repository_artifact.version_conflict.")] int? expected_version = null,
        CancellationToken cancellationToken = default)
    {
        var coordinate = new RepoCoordinate(provider, owner, repo);
        var artifact = await writer.WriteAsync(
            new WriteRepositoryArtifactCommand(
                coordinate,
                slug,
                title,
                document,
                expected_version),
            cancellationToken);
        return McpRepositoryDocumentRef.From(artifact);
    }

    [McpServerTool(Name = "get_repository_document", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read a repository knowledge page by (provider, owner, repo, slug). Returns title, document and version — take `version` as the next write_repository_document `expected_version`. Fails with repository_artifact.not_found when the page does not exist yet.")]
    public async Task<McpRepositoryDocument> GetRepositoryDocument(
        [Description("Git provider of the target repository (e.g. github).")] string provider,
        [Description("Repository owner / organization.")] string owner,
        [Description("Repository name.")] string repo,
        [Description("Page slug to read.")] string slug,
        CancellationToken cancellationToken = default)
    {
        var coordinate = new RepoCoordinate(provider, owner, repo);
        var artifact = await getDocument.HandleAsync(
            new GetRepositoryDocumentQuery(coordinate, slug), cancellationToken);
        return McpRepositoryDocument.From(artifact);
    }

    [McpServerTool(Name = "list_repositories", ReadOnly = true, UseStructuredContent = true)]
    [Description("List every repository the server knows (registered from a bind, a tag default, or a knowledge-page write), each with the slugs of pages already written to it. Use this to confirm a clone's `git remote` coordinate is registered and to see which pages already exist before authoring one. Empty `items` just means nothing is registered yet — not an error.")]
    public async Task<McpRepositoryListResult> ListRepositories(CancellationToken cancellationToken = default)
    {
        var repositories = await listRepositories.HandleAsync(cancellationToken);
        return new McpRepositoryListResult(
            repositories.Select(r => new McpRegisteredRepository(
                r.Coordinate.Provider,
                r.Coordinate.Owner,
                r.Coordinate.Repo,
                r.DocumentSlugs)).ToList());
    }
}

public sealed record McpRepositoryDocumentRef(
    [property: Description("Page slug (unique per repository).")] string Slug,
    [property: Description("Page title.")] string Title,
    [property: Description("Version after this write; pass as expected_version on the next update.")] int Version)
{
    public static McpRepositoryDocumentRef From(RepositoryArtifact a) =>
        new(a.Slug, a.Title, a.Version);
}

public sealed record McpRepositoryDocument(
    [property: Description("Page slug (unique per repository).")] string Slug,
    [property: Description("Page title.")] string Title,
    [property: Description("Full markdown body.")] string Document,
    [property: Description("Current version; pass as expected_version on the next update.")] int Version)
{
    public static McpRepositoryDocument From(RepositoryArtifact a) =>
        new(a.Slug, a.Title, a.Document, a.Version);
}

public sealed record McpRepositoryListResult(
    [property: Description("Registered repositories, ordered by coordinate; empty when none registered.")] IReadOnlyList<McpRegisteredRepository> Items);

public sealed record McpRegisteredRepository(
    [property: Description("Git provider (e.g. github).")] string Provider,
    [property: Description("Repository owner / organization.")] string Owner,
    [property: Description("Repository name.")] string Repo,
    [property: Description("Slugs of knowledge pages already written to this repository.")] IReadOnlyList<string> DocumentSlugs);
