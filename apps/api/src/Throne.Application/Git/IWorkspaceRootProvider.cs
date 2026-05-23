namespace Throne.Application.Git;

/// <summary>
/// Application-side accessor for the absolute, validated workspace root configured via
/// <c>Throne:Workspace:Root</c> (ADR-0024 § 1). Implemented in Infrastructure
/// (<c>WorkspaceRootInitializer</c>) — the boot-time hosted service creates the directory,
/// probes write access and exposes the resolved absolute path here.
///
/// Use this port instead of touching configuration directly: Application/Domain code
/// must not know about <c>IConfiguration</c>, <c>IHostedService</c> ordering or the
/// "~" expansion rules.
/// </summary>
public interface IWorkspaceRootProvider
{
    /// <summary>
    /// Absolute filesystem path that already exists and is writable. Throws
    /// <see cref="InvalidOperationException"/> if read before the boot-time
    /// initializer has run — should never happen at request time.
    /// </summary>
    string ResolvedRoot { get; }
}
