using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Boot-time guard for the workspace root (ADR-0024 § 1 / T-05 ревью D1).
/// On start:
///   1) Reads <c>Throne:Workspace:Root</c> (default <c>~/.throne/workspaces</c>).
///   2) Expands a leading <c>~</c> via <see cref="WorkspacePathExpansion"/>.
///   3) Creates the directory recursively.
///   4) Probes write access via <see cref="WorkspaceWritabilityProbe"/>.
///   5) Throws on failure — Throne refuses to start without a writable workspace.
///
/// The resolved absolute path is exposed via <see cref="ResolvedRoot"/> so other
/// infrastructure components (T-08 / T-09) can compose binding paths off the
/// same value the operator sees in logs.
/// </summary>
internal sealed class WorkspaceRootInitializer(
    IOptions<WorkspaceOptions> options,
    ILogger<WorkspaceRootInitializer> log) : IHostedService, IWorkspaceRootProvider
{
    private string? _resolvedRoot;

    /// <summary>Absolute, validated workspace root. Available after <see cref="StartAsync"/>.</summary>
    public string ResolvedRoot =>
        _resolvedRoot ?? throw new InvalidOperationException(
            "WorkspaceRootInitializer.StartAsync did not run yet.");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var resolved = ResolveRoot(options.Value.Root);
        Directory.CreateDirectory(resolved);
        WorkspaceWritabilityProbe.Verify(resolved, log);

        _resolvedRoot = resolved;
        WorkspaceRootInitializerLog.Resolved(log, resolved);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string ResolveRoot(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"'{WorkspaceOptions.SectionName}:Root' is empty. Set it to an absolute path or '~/...'.");
        }

        return Path.GetFullPath(WorkspacePathExpansion.ExpandHome(configured));
    }
}
