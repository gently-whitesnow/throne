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

    /// <summary>Stable slug of the schema-map knowledge page that turns on <see cref="SchemaMap"/>.</summary>
    public const string SchemaMapSlug = "db-schema-map";

    public static bool IsKnown(string value) => value is Markdown or SchemaMap;

    /// <summary>
    /// Render hint implied by a slug. The write surfaces do not take <c>render_hint</c> on the
    /// wire (ADR-0031): the stable <see cref="SchemaMapSlug"/> page renders as a schema map,
    /// every other page as plain markdown.
    /// </summary>
    public static string ForSlug(string slug) =>
        string.Equals(slug, SchemaMapSlug, StringComparison.Ordinal) ? SchemaMap : Markdown;
}
