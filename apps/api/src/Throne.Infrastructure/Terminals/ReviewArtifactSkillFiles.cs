using System.Globalization;
using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class ReviewArtifactSkillFiles
{
    private const string ScriptRelativePath = "bin/throne-pr-artifact-write";
    private const string ClaudeSkillPath = ".claude/skills/throne-review-artifact/SKILL.md";
    private const string HintFileName = "throne-session.review-artifact.md";

    public static async Task WriteScriptAsync(
        string workspacePath,
        ReviewArtifactSessionSkillPackage package,
        string? apiBaseUrl,
        CancellationToken ct)
    {
        var path = ScriptPath(workspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            BuildScript(NormalizeApiBaseUrl(apiBaseUrl), package.Target),
            Encoding.UTF8,
            ct);
        FileModeHelpers.MakeExecutable(path);
    }

    public static async Task WriteClaudeSkillAsync(
        string workspacePath,
        ReviewArtifactSessionSkillPackage package,
        CancellationToken ct)
    {
        var path = Path.Combine(workspacePath, ClaudeSkillPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, Hint(workspacePath, package), Encoding.UTF8, ct);
    }

    public static async Task<string> WriteOpencodeHintAsync(
        string workspacePath,
        ReviewArtifactSessionSkillPackage package,
        CancellationToken ct)
    {
        var path = Path.Combine(workspacePath, HintFileName);
        await File.WriteAllTextAsync(path, Hint(workspacePath, package), Encoding.UTF8, ct);
        return HintFileName;
    }

    public static string Hint(string workspacePath, ReviewArtifactSessionSkillPackage package) =>
        BuildHint(ScriptPath(workspacePath), package.Target);

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

        Pass one JSON payload on stdin. `content` is the human-readable markdown body;
        `review_recommendation` carries the typed signals the UI consumes (today only AI file
        ordering — impact/provenance fields will be added when a UI consumer materialises, do
        not invent them). `head_sha` is the PR head sha you reviewed — the UI flags the
        artifact stale once the PR moves past it.

        ```json
        {
          "render": "markdown",
          "content": "## Review recommendation\n...",
          "summary": "Short recommendation for the operator",
          "source": "agent",
          "source_refs": ["gh pr diff", "gh pr view --comments"],
          "head_sha": "<PR head commit sha>",
          "review_recommendation": {
            "file_order": [
              { "path": "src/Core.cs", "reason": "core/highest-risk; read first", "risk": "high" },
              { "path": "src/Leaf.cs", "reason": "trivial leaf", "risk": "low" }
            ]
          },
          "produced_at": "2026-06-18T12:00:00Z"
        }
        ```

        Order `file_order` from the most risky/root files to leaves (the reading order for
        review). `risk` is one of `high` | `medium` | `low`.

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
}
