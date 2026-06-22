namespace Throne.Application.Terminals;

/// <summary>
/// Baked-in workspace-discipline paragraph prepended to the operator-curated system prompt for
/// <see cref="TerminalRunModes.Work"/> spawns. Honor-system rule, not a sandbox: tmux spawns with
/// the right cwd but the agent is otherwise free to roam the FS, and a real incident had a codex
/// session diagnosing a live API by patching the operator's runtime checkout instead of its own
/// intent workspace. The paragraph names the workspace path + intent id explicitly so the agent
/// sees its sandbox by absolute path, and asks for chat approval before any out-of-workspace
/// read/write. Vendor-neutral: injected before the adapter assembles its file-backed system
/// prompt (Claude --append-system-prompt-file, Codex -p profile, OpenCode instructions), so both
/// curated prompt-parts (ADR-0034) and the skill appendix (Codex) ride on top of it unchanged.
/// </summary>
public static class WorkspaceDisciplinePrompt
{
    public static string? Compose(string mode, string workspacePath, string intentId, string? curatedSystemPrompt)
    {
        if (!string.Equals(mode, TerminalRunModes.Work, StringComparison.Ordinal))
        {
            return curatedSystemPrompt;
        }

        var paragraph = Paragraph(workspacePath, intentId);
        return string.IsNullOrWhiteSpace(curatedSystemPrompt)
            ? paragraph
            : paragraph + "\n\n" + curatedSystemPrompt;
    }

    private static string Paragraph(string workspacePath, string intentId) =>
        $"Твой intent workspace: `{workspacePath}` (intent id: `{intentId}`). "
        + "Любые правки кода, запуск тестов, артефакты — только внутри этого пути. "
        + "Если для задачи нужно прочитать или изменить что-то снаружи (другой репозиторий, "
        + "runtime-чекаут, домашняя директория, системные файлы) — сначала напиши оператору в "
        + "чате что именно и зачем, и дождись явного «да». Без разрешения наружу не выходи "
        + "даже на чтение.";
}
