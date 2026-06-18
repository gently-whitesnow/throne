using System.Globalization;
using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class ReviewArtifactWorkspaceFiles
{
    private const string ScriptRelativePath = "bin/throne-pr-artifact-write";
    private const string ClaudeSkillPath = ".claude/skills/throne-review-artifact/SKILL.md";
    private const string HintFileName = "throne-session.review-artifact.md";

    public static async Task WriteScriptAsync(
        string workspacePath,
        ReviewArtifactWriteTarget target,
        string? apiBaseUrl,
        CancellationToken ct)
    {
        var path = ScriptPath(workspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            BuildScript(NormalizeApiBaseUrl(apiBaseUrl), target),
            Encoding.UTF8,
            ct);
        MakeExecutable(path);
    }

    public static async Task WriteClaudeSkillAsync(
        string workspacePath,
        ReviewArtifactWriteTarget target,
        CancellationToken ct)
    {
        var path = Path.Combine(workspacePath, ClaudeSkillPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            BuildHint(ScriptPath(workspacePath), target),
            Encoding.UTF8,
            ct);
    }

    public static string WithCodexHint(
        string? systemPrompt,
        string workspacePath,
        ReviewArtifactWriteTarget? target)
    {
        var prompt = systemPrompt ?? string.Empty;
        if (target is null)
        {
            return prompt;
        }

        var hint = BuildHint(ScriptPath(workspacePath), target);
        return string.IsNullOrWhiteSpace(prompt)
            ? hint
            : $"{prompt.TrimEnd()}\n\n{hint}";
    }

    public static async Task<string?> WriteOpencodeHintAsync(
        string workspacePath,
        ReviewArtifactWriteTarget? target,
        CancellationToken ct)
    {
        if (target is null)
        {
            return null;
        }

        var path = Path.Combine(workspacePath, HintFileName);
        await File.WriteAllTextAsync(path, BuildHint(ScriptPath(workspacePath), target), Encoding.UTF8, ct);
        return HintFileName;
    }

    private static string ScriptPath(string workspacePath) =>
        Path.Combine(workspacePath, ScriptRelativePath);

    private static string BuildHint(string scriptPath, ReviewArtifactWriteTarget target) =>
        $$"""
        # Throne review artifact writer

        This review session can update the PR artifact `review_recommendation` through:

        ```bash
        {{scriptPath}}
        ```

        The script is the canonical write path for this session. It is already bound to
        `binding_id={{target.BindingId}}`, PR `#{{target.PullRequestNumber}}`, artifact
        `type={{ReviewArtifactWriteTarget.ArtifactType}}`, and the local Throne API.

        Pass one JSON payload on stdin:

        ```json
        {
          "render": "markdown",
          "content": "## Review recommendation\n...",
          "summary": "Short recommendation for the operator",
          "source": "agent",
          "source_refs": ["gh pr diff", "gh pr view --comments"],
          "produced_at": "2026-06-18T12:00:00Z"
        }
        ```

        If gate `send-comments` is enabled, post actionable per-file/per-line review
        comments to the provider with `gh` or `glab`. If it is disabled, keep those
        comments in the session chat only. Do not store provider comments locally.
        """;

    private static string BuildScript(string apiBaseUrl, ReviewArtifactWriteTarget target) =>
        $$"""
        #!/usr/bin/env bash
        set -euo pipefail

        API_BASE={{Sh(apiBaseUrl)}}
        BINDING_ID={{Sh(target.BindingId)}}
        PR_NUMBER={{Sh(target.PullRequestNumber.ToString(CultureInfo.InvariantCulture))}}
        ARTIFACT_TYPE={{Sh(ReviewArtifactWriteTarget.ArtifactType)}}

        if [[ "${1:-}" == "--help" ]]; then
          cat <<USAGE
        Writes Throne PR artifact ${ARTIFACT_TYPE} for binding ${BINDING_ID}, PR #${PR_NUMBER}.
        Usage: throne-pr-artifact-write < payload.json
        USAGE
          exit 0
        fi

        payload="$(cat)"
        if [[ -z "${payload//[[:space:]]/}" ]]; then
          echo "throne-pr-artifact-write: expected JSON payload on stdin" >&2
          exit 64
        fi

        url="${API_BASE}/api/v1/repositories/${BINDING_ID}/artifacts/${ARTIFACT_TYPE}"
        tmp="$(mktemp)"
        status="$(printf '%s' "${payload}" | curl -sS -o "${tmp}" -w '%{http_code}' \
          -X PUT "${url}" \
          -H 'Content-Type: application/json' \
          --data-binary @-)"
        if [[ "${status}" =~ ^2 ]]; then
          cat "${tmp}"
          rm -f "${tmp}"
          exit 0
        fi

        cat "${tmp}" >&2
        rm -f "${tmp}"
        echo "throne-pr-artifact-write: PUT failed with HTTP ${status}" >&2
        exit 1
        """;

    private static string NormalizeApiBaseUrl(string? apiBaseUrl) =>
        string.IsNullOrWhiteSpace(apiBaseUrl)
            ? SessionHookOptions.DefaultApiBaseUrl
            : apiBaseUrl.TrimEnd('/');

    private static string Sh(string value) => $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }
}
