namespace Throne.Domain.Repositories;

/// <summary>Wire-format constants for <see cref="PullRequestArtifact.Render"/>.</summary>
public static class PullRequestArtifactRenderNames
{
    public const string Markdown = "markdown";
    public const string Mermaid = "mermaid";
    public const string Svg = "svg";
    public const string Json = "json";

    public static bool IsKnown(string value) =>
        value is Markdown or Mermaid or Svg or Json;
}
