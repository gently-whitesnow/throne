using System.Text;
using Throne.Application.Intents;
using Throne.Domain.TaskTrackers;

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
/// opens with <c>Read</c>), attached card snapshots and the session skills the operator loaded for
/// this mode. These are context the agent reads, not text it edits — keeping them out of the
/// round-tripping user_prompt.
/// </summary>
internal static class WorkspaceMapPrompt
{
    public static string Compose(
        string workspaceRoot,
        IReadOnlyList<string> repoPaths,
        IReadOnlyList<string> tags,
        IReadOnlyList<IntentLinkPromptContext> links,
        IReadOnlyList<IntentAttachment> attachments,
        IReadOnlyList<IntentCardAttachment> cardAttachments,
        IReadOnlyList<string> sessionSkillIds,
        string userPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var map = new StringBuilder();
        map.Append("=== Карта workspace ===\n");
        map.Append("Корень workspace: ").Append(workspaceRoot).Append('\n');
        AppendRepositories(map, repoPaths);
        AppendCwdModel(map);
        AppendTags(map, tags);
        AppendLinks(map, links);
        AppendSkills(map, sessionSkillIds);
        AppendAttachments(map, attachments);
        AppendCards(map, cardAttachments);

        map.Append("=======================\n\n");
        map.Append(userPrompt);
        return map.ToString();
    }

    private static void AppendRepositories(StringBuilder map, IReadOnlyList<string> repoPaths)
    {
        if (repoPaths is not { Count: > 0 })
        {
            map.Append("Репозитории: не смонтированы.\n");
            return;
        }
        map.Append("Репозитории:\n");
        foreach (var path in repoPaths)
        {
            map.Append("- ").Append(path).Append('\n');
        }
    }

    private static void AppendCwdModel(StringBuilder map)
    {
        map.Append("Остальное в корне workspace — session-метадата (.claude/, skills/, throne-session.*), ");
        map.Append("не часть репозитория.\n");
        map.Append("Путь к репозиторию бери отсюда (абсолютный) — не угадывай имя клон-сабдира.\n");
        map.Append("cwd между Bash-вызовами не гарантирована — может сбрасываться к корню workspace.\n");
        map.Append("Не полагайся на `cd` из прошлого вызова: в каждой команде префиксуй абсолютный путь репо ");
        map.Append("или `cd <абсолютный путь репо> && …`.\n");
    }

    private static void AppendTags(StringBuilder map, IReadOnlyList<string> tags)
    {
        if (tags is { Count: > 0 })
        {
            map.Append("Теги интента: ").Append(string.Join(", ", tags)).Append('\n');
        }
    }

    private static void AppendLinks(StringBuilder map, IReadOnlyList<IntentLinkPromptContext> links)
    {
        if (links is not { Count: > 0 })
        {
            return;
        }
        map.Append("Связи:\n");
        foreach (var link in links)
        {
            map.Append("- ").Append(link.Label).Append(" intent_id=").Append(link.PeerIntentId);
            map.Append(" status=").Append(link.Status);
            map.Append(string.IsNullOrWhiteSpace(link.Rationale)
                ? " (без причины связи)"
                : $": {link.Rationale}");
            map.Append('\n');
        }
    }

    private static void AppendSkills(StringBuilder map, IReadOnlyList<string> sessionSkillIds)
    {
        if (sessionSkillIds is { Count: > 0 })
        {
            map.Append("Скиллы сессии: ").Append(string.Join(", ", sessionSkillIds)).Append('\n');
        }
    }

    private static void AppendAttachments(StringBuilder map, IReadOnlyList<IntentAttachment> attachments)
    {
        if (attachments is not { Count: > 0 })
        {
            return;
        }
        map.Append("Приложения интента (открой через Read):\n");
        foreach (var att in attachments)
        {
            map.Append("- \"").Append(EscapeFileName(att.FileName)).Append("\": ")
                .Append(WorkspaceAttachmentPaths.RelativePath(att.Id, att.FileName))
                .Append('\n');
        }
    }

    private static void AppendCards(StringBuilder map, IReadOnlyList<IntentCardAttachment> cards)
    {
        if (cards is not { Count: > 0 })
        {
            return;
        }
        map.Append("Приложенные карточки интента:\n");
        foreach (var card in cards)
        {
            var snapshot = card.State.Snapshot;
            map.Append("[card ")
                .Append(card.Coordinate.Tracker).Append('/')
                .Append(card.Coordinate.BoardId).Append('/')
                .Append(card.Coordinate.CardId).Append(']');
            if (snapshot.Archived)
            {
                map.Append(" (в архиве)");
            }
            map.Append('\n');
            map.Append("Title: ").Append(snapshot.Title).Append('\n');
            map.Append("ColumnTitle: ").Append(snapshot.ColumnTitle ?? "").Append('\n');
            map.Append("Description:\n").Append(snapshot.Description ?? "").Append('\n');
        }
    }

    private static string EscapeFileName(string fileName) =>
        fileName.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}
