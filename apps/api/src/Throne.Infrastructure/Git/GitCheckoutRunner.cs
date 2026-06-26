using System.ComponentModel;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Провайдер-нейтральный checkout ветки через plain <c>git</c> (тот же seam
/// <see cref="IProcessLauncher"/>, что и <see cref="LocalGitWorkspaceSync"/>). Используется
/// post-clone шагом обоих провайдеров для ветки-override из биндинга. PR-путь сюда не заходит —
/// PR форка переключается провайдерным CLI (`gh pr checkout` / `glab mr checkout`).
/// Контракт мягкий: ветка-плейсхолдер "main" на репо с дефолтом master не должна ронять клон,
/// поэтому отсутствие ref на origin — тихий no-op, а не ошибка.
/// </summary>
internal sealed class GitCheckoutRunner(IProcessLauncher launcher)
{
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(30);

    public async Task CheckoutBranchAsync(string workspacePath, string? branch, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        if (string.IsNullOrWhiteSpace(branch))
        {
            return;
        }

        // Уже на нужной ветке (свежий клон встал на неё) — не дёргаем рабочее дерево.
        var current = await RunAsync(workspacePath, ["rev-parse", "--abbrev-ref", "HEAD"], "rev-parse", ct);
        if (string.Equals(current.StandardOutput.Trim(), branch, StringComparison.Ordinal))
        {
            return;
        }

        // rev-parse --verify легитимно возвращает ненулевой код, когда ref отсутствует —
        // это плейсхолдер "main" на репо с дефолтом master. Обрабатываем по ExitCode, не кидаем.
        var verify = await RunAllowingFailureAsync(
            workspacePath, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{branch}"], "rev-parse", ct);
        if (!verify.IsSuccess)
        {
            return;
        }

        var result = await RunAllowingFailureAsync(workspacePath, ["checkout", branch], "checkout", ct);
        if (!result.IsSuccess)
        {
            throw new GitProviderException(
                GitProviderErrorKind.CliFailure,
                $"git checkout ветки '{branch}' завершился с кодом {result.ExitCode}.",
                result.StandardError.Trim());
        }
    }

    /// <summary>
    /// Запуск git с маппингом сбоев процесса (таймаут / нет executable) в
    /// <see cref="GitProviderException"/> и проверкой нулевого кода возврата.
    /// </summary>
    private async Task<ProcessRunResult> RunAsync(
        string workspacePath, IReadOnlyList<string> operation, string label, CancellationToken ct)
    {
        var result = await RunAllowingFailureAsync(workspacePath, operation, label, ct);
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
    /// Запуск git без проверки кода возврата — для команд (rev-parse --verify), где
    /// ненулевой exit означает «ref отсутствует», а не сбой. Сбои самого процесса
    /// (таймаут / нет executable) всё равно мапятся в <see cref="GitProviderException"/>.
    /// </summary>
    private async Task<ProcessRunResult> RunAllowingFailureAsync(
        string workspacePath, IReadOnlyList<string> operation, string label, CancellationToken ct)
    {
        string[] arguments = ["-C", workspacePath, .. operation];
        try
        {
            return await launcher.RunAsync(
                new ProcessRunRequest(FileName: "git", Arguments: arguments, Timeout: QuickTimeout), ct);
        }
        catch (TimeoutException ex)
        {
            throw new GitProviderException(
                GitProviderErrorKind.NetworkError, $"git {label} превысил тайм-аут {QuickTimeout}.", null, ex);
        }
        catch (Win32Exception ex)
        {
            throw new GitProviderException(
                GitProviderErrorKind.CliFailure, "git executable not found on PATH.", ex.Message, ex);
        }
    }
}
