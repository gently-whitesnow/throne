using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git;

/// <summary>
/// <see cref="ILocalGitBranchReader"/> backed by a <c>git rev-parse --abbrev-ref HEAD</c>
/// shell-out through <see cref="IProcessLauncher"/> (same seam as the <c>gh</c> CLI per
/// ADR-0024 § 3). Never throws on a bad clone — any non-zero exit, detached HEAD, or
/// missing git binary collapses to <see langword="null"/> so the auto-bind pass simply
/// skips the binding.
/// </summary>
internal sealed class LocalGitBranchReader(IProcessLauncher launcher) : ILocalGitBranchReader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<string?> ReadCurrentBranchAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        ProcessRunResult result;
        try
        {
            result = await launcher.RunAsync(
                new ProcessRunRequest(
                    FileName: "git",
                    Arguments: ["-C", workspacePath, "rev-parse", "--abbrev-ref", "HEAD"],
                    Timeout: Timeout),
                ct);
        }
        catch (Exception) when (ct.IsCancellationRequested is false)
        {
            return null;
        }

        if (!result.IsSuccess)
        {
            return null;
        }

        var branch = result.StandardOutput.Trim();
        return branch.Length == 0 || string.Equals(branch, "HEAD", StringComparison.Ordinal)
            ? null
            : branch;
    }
}
