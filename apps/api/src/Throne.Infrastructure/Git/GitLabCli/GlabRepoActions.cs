using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabRepoActions(
    GlabCliInvoker glab,
    IGitLabHostProvider hostProvider,
    IOptions<GitLabCliOptions> options,
    IProcessLauncher launcher)
{
    private readonly GitLabCliOptions _opts = options.Value;

    public async Task CloneAsync(string owner, string repo, string targetPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        if (TryReuseExistingClone(targetPath))
        {
            return;
        }

        var host = await hostProvider.GetHostAsync(ct);
        var path = GlabProjectPath.FullPath(owner, repo);
        var result = await glab.RunCloneAsync(
            ["repo", "clone", path, targetPath, "--", "--filter=blob:none"],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            throw GlabExceptions.FromExit($"repo clone {path}", result);
        }
    }

    public async Task SyncAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var host = await hostProvider.GetHostAsync(ct);
        var result = await RunGitAsync(
            workspacePath,
            ["fetch", "--all", "--prune"],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            throw GlabExceptions.FromExit($"git fetch in {workspacePath}", result);
        }
    }

    private async Task<ProcessRunResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken ct)
    {
        try
        {
            return await launcher.RunAsync(
                new ProcessRunRequest(
                    FileName: _opts.GitExecutablePath,
                    Arguments: arguments,
                    WorkingDirectory: workingDirectory,
                    Environment: environment,
                    Timeout: _opts.DefaultTimeout),
                ct);
        }
        catch (TimeoutException ex)
        {
            throw GlabExceptions.Timeout(_opts.DefaultTimeout, ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw GlabExceptions.ToolExecutableMissing("git", _opts.GitExecutablePath, ex);
        }
    }

    private static bool TryReuseExistingClone(string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            return false;
        }

        if (Directory.Exists(Path.Combine(targetPath, ".git")))
        {
            return true;
        }

        if (!Directory.EnumerateFileSystemEntries(targetPath).Any())
        {
            Directory.Delete(targetPath);
            return false;
        }

        throw new GitProviderException(
            GitProviderErrorKind.CliFailure,
            $"workspace path '{targetPath}' already exists and is not a git clone; remove it manually before binding the repository again");
    }
}
