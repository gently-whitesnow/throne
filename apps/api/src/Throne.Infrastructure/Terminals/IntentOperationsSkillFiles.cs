using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class IntentOperationsSkillFiles
{
    private const string ScriptRelativePath = "bin/throne-intent";
    private const string TextSkillPath = ".claude/skills/throne-intent-text/SKILL.md";
    private const string ChildSkillPath = ".claude/skills/throne-intent-decompose/SKILL.md";
    private const string HintFileName = "throne-session.intent-ops.md";

    public static async Task WriteScriptAsync(
        string workspacePath,
        IntentOperationsSessionSkillPackage package,
        string? apiBaseUrl,
        CancellationToken ct)
    {
        var path = ScriptPath(workspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            IntentOperationsScriptTemplate.Build(package.IntentId, NormalizeApiBaseUrl(apiBaseUrl)),
            FileModeHelpers.ScriptEncoding,
            ct);
        FileModeHelpers.MakeExecutable(path);
    }

    public static async Task WriteClaudeSkillsAsync(
        string workspacePath,
        IntentOperationsSessionSkillPackage package,
        CancellationToken ct)
    {
        var scriptPath = ScriptPath(workspacePath);
        await WriteClaudeSkillAsync(workspacePath, TextSkillPath, TextSkill(scriptPath), ct);
        await WriteClaudeSkillAsync(workspacePath, ChildSkillPath, ChildSkill(scriptPath), ct);
    }

    public static async Task<string> WriteOpencodeHintAsync(
        string workspacePath,
        IntentOperationsSessionSkillPackage package,
        CancellationToken ct)
    {
        var path = Path.Combine(workspacePath, HintFileName);
        await File.WriteAllTextAsync(path, Hint(workspacePath, package), Encoding.UTF8, ct);
        return HintFileName;
    }

    public static string Hint(string workspacePath, IntentOperationsSessionSkillPackage package) =>
        $$"""
        # Throne intent operations

        This interview session can edit the current Intent and create/link child Intents through:

        ```bash
        {{ScriptPath(workspacePath)}}
        ```

        This script is the write path for editing the current Intent and creating/linking child
        Intents in this session. It is already bound to `intent_id={{package.IntentId}}`.
        `replace-text` reads the current intent first, supplies `expected_version` internally, and
        handles a 409 by asking you to refresh the fragments rather than making you manage version
        numbers.

        Common commands:

        ```bash
        {{ScriptPath(workspacePath)}} get
        {{ScriptPath(workspacePath)}} replace-text --old-file /tmp/old.txt --new-file /tmp/new.txt
        child_id="$({{ScriptPath(workspacePath)}} create --text-file /tmp/child.md --id-only)"
        {{ScriptPath(workspacePath)}} link "$child_id" --blocking false --rationale-file /tmp/rationale.txt
        ```
        """;

    private static async Task WriteClaudeSkillAsync(
        string workspacePath,
        string relativePath,
        string content,
        CancellationToken ct)
    {
        var path = Path.Combine(workspacePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
    }

    private static string TextSkill(string scriptPath) =>
        $$"""
        # Throne intent text editor

        Use `{{scriptPath}}` when this interview needs to update the current `Intent.text`.

        Workflow:

        ```bash
        {{scriptPath}} get
        printf '%s' '<exact old fragment>' > /tmp/throne-old.txt
        printf '%s' '<replacement fragment>' > /tmp/throne-new.txt
        {{scriptPath}} replace-text --old-file /tmp/throne-old.txt --new-file /tmp/throne-new.txt
        ```

        The old fragment must occur exactly once in the current text. Do not pass or track
        `expected_version`; the script reads it immediately before the write.
        """;

    private static string ChildSkill(string scriptPath) =>
        $$"""
        # Throne child intent decomposition

        Use `{{scriptPath}}` when this interview decomposes the current intent into child intents.

        Workflow:

        ```bash
        printf '%s' '<child intent text>' > /tmp/throne-child.md
        child_id="$({{scriptPath}} create --text-file /tmp/throne-child.md --id-only)"
        printf '%s' '<why this child belongs under the current intent>' > /tmp/throne-rationale.txt
        {{scriptPath}} link "$child_id" --blocking false --rationale-file /tmp/throne-rationale.txt
        ```

        Use `--tag <name>` on `create` only when the child should start with explicit tags.
        Use `--blocking true` only for a hard dependency edge; otherwise keep it `false`.
        """;

    private static string ScriptPath(string workspacePath) =>
        Path.Combine(workspacePath, ScriptRelativePath);

    private static string NormalizeApiBaseUrl(string? apiBaseUrl) =>
        string.IsNullOrWhiteSpace(apiBaseUrl)
            ? SessionHookOptions.DefaultApiBaseUrl
            : apiBaseUrl.TrimEnd('/');
}
