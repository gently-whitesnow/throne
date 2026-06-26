using System.Globalization;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// File-system level git actions (<c>gh repo clone</c>, <c>gh repo sync</c>)
/// performed by <see cref="GitHubCliProvider"/>.
/// </summary>
internal sealed class GhRepoActions(GhCliInvoker gh, GitCheckoutRunner gitCheckout)
{
    public async Task CloneAsync(
        string owner, string repo, string targetPath, CloneCheckout checkout, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(checkout);

        // unbind не удаляет клон с диска (изоляция бранчей), но повторный bind
        // на ту же пару должен пройти. Если папка уже git-репо — переиспользуем
        // её, иначе чистим пустой каталог и клонируем. В обоих случаях после этого
        // применяем checkout, чтобы reuse не вставал мимо выбранного PR/ветки.
        if (!TryReuseExistingClone(targetPath))
        {
            // ADR-0026 §5: partial clone (`--filter=blob:none`). Метаданные + история тянутся
            // мгновенно, blob'ы — on-demand при `git checkout`/`bisect`/open. Без этого Run
            // pre-flight на больших monorepo блокирует tmux-spawn на минуты.
            // `gh repo clone owner/repo path -- --filter=blob:none` — флаги после `--`
            // прокидываются в `git clone` без обёртки.
            var result = await gh.RunCloneAsync(
                ["repo", "clone", $"{owner}/{repo}", targetPath, "--", "--filter=blob:none"],
                ct);
            if (!result.IsSuccess)
            {
                throw GhExceptions.FromExit($"repo clone {owner}/{repo}", result);
            }
        }

        await ApplyCheckoutAsync(targetPath, checkout, ct);
    }

    public Task CheckoutAsync(
        string owner, string repo, string workspacePath, CloneCheckout checkout, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(checkout);
        return ApplyCheckoutAsync(workspacePath, checkout, ct);
    }

    private async Task ApplyCheckoutAsync(string workspacePath, CloneCheckout checkout, CancellationToken ct)
    {
        if (checkout.PullRequestNumber is int n)
        {
            // `gh pr checkout` корректно тянет PR из форка (настраивает remote/upstream).
            var result = await gh.RunInAsync(
                workspacePath, ["pr", "checkout", n.ToString(CultureInfo.InvariantCulture)], ct);
            if (!result.IsSuccess)
            {
                throw GhExceptions.FromExit($"pr checkout #{n}", result);
            }
            return;
        }

        await gitCheckout.CheckoutBranchAsync(workspacePath, checkout.Branch, ct);
    }

    public async Task SyncAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

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
