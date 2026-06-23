using System.Text;

namespace Throne.Application.Terminals;

/// <summary>
/// Prepends a workspace map to the delivered task prompt: the absolute workspace root, the absolute
/// path of every mounted repo clone, a note that everything else in the root is session metadata,
/// and the intent's tags as light context. Counters the dominant embedded-session failure where the
/// agent guesses the clone sub-directory name and <c>cd</c>s into a path that does not exist
/// (ADR-0026); the agent reads the real paths instead of inventing them. Paths only — never file
/// contents.
/// </summary>
internal static class WorkspaceMapPrompt
{
    public static string Compose(
        string workspaceRoot,
        IReadOnlyList<string> repoPaths,
        IReadOnlyList<string> tags,
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
        if (tags is { Count: > 0 })
        {
            map.Append("Теги интента: ").Append(string.Join(", ", tags)).Append('\n');
        }

        map.Append("=======================\n\n");
        map.Append(userPrompt);
        return map.ToString();
    }
}
