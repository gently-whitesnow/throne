namespace Throne.Domain.Repositories;

/// <summary>
/// Wire-format constants for <see cref="RepositoryArtifact.RenderHint"/> (ADR-0031).
/// <see cref="Markdown"/> is the default; <see cref="SchemaMap"/> turns on the mermaid
/// erDiagram affordances used by the stable <c>db-schema-map</c> page.
/// </summary>
public static class RepositoryArtifactRenderHints
{
    public const string Markdown = "markdown";
    public const string SchemaMap = "schema_map";

    public static bool IsKnown(string value) => value is Markdown or SchemaMap;
}
