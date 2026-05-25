using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// File-system level git actions (<c>gh repo clone</c>, <c>gh repo sync</c>)
/// performed by <see cref="GitHubCliProvider"/>. Extracted so the provider stays
/// inside the CA1502 cyclomatic budget and so the clone-queue (T-09) can stub
/// just these operations independently of repo search.
/// </summary>
internal sealed class GhRepoActions(GhCliInvoker gh)
{
    public async Task CloneAsync(string owner, string repo, string targetPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        // unbind не удаляет клон с диска (см. slice 1 Q2 — изоляция бранчей), но
        // повторный bind на ту же пару должен пройти. Если папка уже git-репо —
        // переиспользуем её, иначе чистим пустой каталог и клонируем.
        if (TryReuseExistingClone(targetPath))
        {
            return;
        }

        var result = await gh.RunCloneAsync(["repo", "clone", $"{owner}/{repo}", targetPath], ct);
        if (!result.IsSuccess)
        {
            throw GhExceptions.FromExit($"repo clone {owner}/{repo}", result);
        }
    }

    public async Task FetchAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        // ADR-0024 § 3 keeps shell-out behind a single launcher; `gh repo sync`
        // wraps `git fetch` with the same auth/env that cloned the repo.
        var result = await gh.RunInAsync(workspacePath, ["repo", "sync"], ct);
        if (!result.IsSuccess)
        {
            throw GhExceptions.FromExit($"repo sync in {workspacePath}", result);
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

        // Пустую папку убираем, чтобы gh смог склонировать в неё без exit 128.
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
