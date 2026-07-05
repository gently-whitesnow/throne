using System.Text;
using Throne.Application.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Prepends a workspace map to the delivered task prompt: the absolute workspace root, the absolute
/// path of every mounted repo clone, a note that everything else in the root is session metadata,
/// and the intent's tags as light context. Counters the dominant embedded-session failure where the
/// agent guesses the clone sub-directory name and <c>cd</c>s into a path that does not exist
/// (ADR-0026); the agent reads the real paths instead of inventing them. Also states the cwd model
/// — cwd is not preserved across Bash calls and resets to the workspace root — so the agent prefixes
/// an absolute repo path in every command instead of relying on a prior <c>cd</c>. Paths only —
/// never file contents.
///
/// Two environment blocks live here rather than in the editable task body: the intent's attachments
/// (staged to <c>.throne/attachments/</c> on spawn, listed here as name + relative path the agent
/// opens with <c>Read</c>) and the session skills the operator loaded for this mode. Both are context
/// the agent reads, not text it edits — keeping them out of the round-tripping user_prompt.
/// </summary>
internal static class WorkspaceMapPrompt
{
    public static string Compose(
        string workspaceRoot,
        IReadOnlyList<string> repoPaths,
        IReadOnlyList<string> tags,
        string? title,
        IReadOnlyList<IntentLinkPromptContext> links,
        IReadOnlyList<IntentAttachment> attachments,
        IReadOnlyList<string> sessionSkillIds,
        string userPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var map = new StringBuilder();
        map.Append("=== Карта workspace ===\n");
        map.Append("Корень workspace: ").Append(workspaceRoot).Append('\n');
        if (repoPaths is { Count: > 0 })
        {
            map.Append("Репозитории:\n");
            foreach (var path in repoPaths)
            {
                map.Append("- ").Append(path).Append('\n');
            }
        }
        else
        {
            map.Append("Репозитории: не смонтированы.\n");
        }

        map.Append("Остальное в корне workspace — session-метадата (.claude/, skills/, throne-session.*), ");
        map.Append("не часть репозитория.\n");
        map.Append("Путь к репозиторию бери отсюда (абсолютный) — не угадывай имя клон-сабдира.\n");
        map.Append("cwd между Bash-вызовами не гарантирована — может сбрасываться к корню workspace.\n");
        map.Append("Не полагайся на `cd` из прошлого вызова: в каждой команде префиксуй абсолютный путь репо ");
        map.Append("или `cd <абсолютный путь репо> && …`.\n");
        if (!string.IsNullOrWhiteSpace(title))
        {
            map.Append("Заголовок интента: ").Append(title).Append('\n');
        }
        if (tags is { Count: > 0 })
        {
            map.Append("Теги интента: ").Append(string.Join(", ", tags)).Append('\n');
        }
        if (links is { Count: > 0 })
        {
            map.Append("Связи:\n");
            foreach (var link in links)
            {
                map.Append("- ").Append(link.Label).Append(" intent_id=").Append(link.PeerIntentId);
                map.Append(" status=").Append(link.Status);
                if (string.IsNullOrWhiteSpace(link.Rationale))
                {
                    map.Append(" (без причины связи)");
                }
                else
                {
                    map.Append(": ").Append(link.Rationale);
                }
                map.Append('\n');
            }
        }

        if (sessionSkillIds is { Count: > 0 })
        {
            map.Append("Скиллы сессии: ").Append(string.Join(", ", sessionSkillIds)).Append('\n');
        }
        if (attachments is { Count: > 0 })
        {
            map.Append("Приложения интента (открой через Read):\n");
            foreach (var att in attachments)
            {
                map.Append("- \"").Append(EscapeFileName(att.FileName)).Append("\": ")
                    .Append(WorkspaceAttachmentPaths.RelativePath(att.Id, att.FileName))
                    .Append('\n');
            }
        }

        map.Append("=======================\n\n");
        map.Append(userPrompt);
        return map.ToString();
    }

    private static string EscapeFileName(string fileName) =>
        fileName.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}
