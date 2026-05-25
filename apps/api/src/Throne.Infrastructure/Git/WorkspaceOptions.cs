namespace Throne.Infrastructure.Git;

/// <summary>
/// Bind target for the <c>Throne:Workspace</c> configuration section.
/// Per ADR-0024 § 1 the default root is <c>~/.throne/workspaces</c>; users may
/// relocate it on machines where the home directory lives on a small SSD.
/// </summary>
public sealed class WorkspaceOptions
{
    public const string SectionName = "Throne:Workspace";

    /// <summary>
    /// Root directory under which every binding gets a
    /// <c>intents/{intent_id}/{owner}__{repo}/</c> subtree. Leading <c>~</c> is
    /// expanded to the current user's home directory by
    /// <see cref="WorkspaceRootInitializer"/>.
    /// </summary>
    public string Root { get; set; } = "~/.throne/workspaces";

    /// <summary>
    /// Optional host-side path for <see cref="Root"/>. Set in docker-compose so the UI
    /// can show the operator where workspace clones live on their machine; empty
    /// outside docker.
    /// </summary>
    public string? HostRoot { get; set; }
}
