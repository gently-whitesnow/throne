using System.ComponentModel;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git;

/// <summary>
/// <see cref="ILocalGitWorkspaceSync"/> backed by plain <c>git</c> shell-outs through
/// <see cref="IProcessLauncher"/> (same seam as <see cref="LocalGitBranchReader"/> and the
/// vendor CLIs per ADR-0024 § 3). Auth for the fetch rides on the credential helper the
/// provider CLI configured at clone-time, so no <c>gh</c>/<c>glab</c> dependency is needed
/// here. Every failure is surfaced as a <see cref="GitProviderException"/> — the action is
/// explicit and user-triggered, so errors must reach the UI (unlike the auto-bind branch
/// reader, which swallows them).
/// </summary>
internal sealed class LocalGitWorkspaceSync(IProcessLauncher launcher) : ILocalGitWorkspaceSync
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(15);

    public async Task SyncCurrentBranchToRemoteAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var branch = await ReadCurrentBranchAsync(workspacePath, ct);
        // fork-PR: ветка трекает upstream на remote форка, не на origin — поэтому тянем все
        // remotes и сбрасываемся на upstream (`@{u}`), а не жёстко на `origin/{branch}`.
        await RunAsync(workspacePath, ["fetch", "--prune", "--all"], FetchTimeout, "fetch", ct);
        var resetTarget = await ResolveResetTargetAsync(workspacePath, branch, ct);
        await RunAsync(workspacePath, ["reset", "--hard", resetTarget], QuickTimeout, "reset", ct);
    }

    /// <summary>
    /// Куда жёстко сбрасывать рабочее дерево: upstream текущей ветки (`@{u}`, корректно для
    /// fork-PR, где upstream живёт на remote форка). Если upstream не настроен, rev-parse
    /// вернёт ненулевой код — это норма (обрабатываем по ExitCode, не кидаем), и мы
    /// откатываемся к прежнему поведению `origin/{branch}`.
    /// </summary>
    private async Task<string> ResolveResetTargetAsync(string workspacePath, string branch, CancellationToken ct)
    {
        var upstream = await RunAllowingFailureAsync(
            workspacePath, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], QuickTimeout, "rev-parse", ct);
        return upstream.IsSuccess && upstream.StandardOutput.Trim().Length > 0
            ? upstream.StandardOutput.Trim()
            : $"origin/{branch}";
    }

    private async Task<string> ReadCurrentBranchAsync(string workspacePath, CancellationToken ct)
    {
        var result = await RunAsync(
            workspacePath, ["rev-parse", "--abbrev-ref", "HEAD"], QuickTimeout, "rev-parse", ct);
        var branch = result.StandardOutput.Trim();
        if (branch.Length == 0 || string.Equals(branch, "HEAD", StringComparison.Ordinal))
        {
            throw new GitProviderException(
                GitProviderErrorKind.CliFailure,
                "Локальный клон в состоянии detached HEAD — нет ветки для синхронизации.",
                $"git rev-parse returned '{branch}' in {workspacePath}");
        }

        return branch;
    }

    private async Task<ProcessRunResult> RunAsync(
        string workspacePath,
        IReadOnlyList<string> operation,
        TimeSpan timeout,
        string label,
        CancellationToken ct)
    {
        var result = await RunAllowingFailureAsync(workspacePath, operation, timeout, label, ct);
        if (!result.IsSuccess)
        {
            throw new GitProviderException(
                GitProviderErrorKind.CliFailure,
                $"git {label} завершился с кодом {result.ExitCode}.",
                result.StandardError.Trim());
        }

        return result;
    }

    /// <summary>
    /// Запуск git без проверки кода возврата — для команд (rev-parse @{u}), где ненулевой
    /// exit означает «upstream не настроен», а не сбой. Сбои самого процесса (таймаут / нет
    /// executable) всё равно мапятся в <see cref="GitProviderException"/>.
    /// </summary>
    private async Task<ProcessRunResult> RunAllowingFailureAsync(
        string workspacePath,
        IReadOnlyList<string> operation,
        TimeSpan timeout,
        string label,
        CancellationToken ct)
    {
        string[] arguments = ["-C", workspacePath, .. operation];
        try
        {
            return await launcher.RunAsync(
                new ProcessRunRequest(FileName: "git", Arguments: arguments, Timeout: timeout), ct);
        }
        catch (TimeoutException ex)
        {
            throw new GitProviderException(
                GitProviderErrorKind.NetworkError, $"git {label} превысил тайм-аут {timeout}.", null, ex);
        }
        catch (Win32Exception ex)
        {
            throw new GitProviderException(
                GitProviderErrorKind.CliFailure, "git executable not found on PATH.", ex.Message, ex);
        }
    }
}
