using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class SessionSkillWorkspaceFiles
{
    public static async Task WriteScriptsAsync(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages,
        string? apiBaseUrl,
        CancellationToken ct)
    {
        foreach (var package in packages)
        {
            switch (package)
            {
                case ReviewArtifactSessionSkillPackage review:
                    await ReviewArtifactSkillFiles.WriteScriptAsync(workspacePath, review, apiBaseUrl, ct);
                    break;
                case IntentOperationsSessionSkillPackage intentOps:
                    await IntentOperationsSkillFiles.WriteScriptAsync(workspacePath, intentOps, apiBaseUrl, ct);
                    break;
            }
        }
    }

    public static async Task WriteClaudeSkillsAsync(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct)
    {
        foreach (var package in packages)
        {
            switch (package)
            {
                case ReviewArtifactSessionSkillPackage review:
                    await ReviewArtifactSkillFiles.WriteClaudeSkillAsync(workspacePath, review, ct);
                    break;
                case IntentOperationsSessionSkillPackage intentOps:
                    await IntentOperationsSkillFiles.WriteClaudeSkillsAsync(workspacePath, intentOps, ct);
                    break;
            }
        }
    }

    public static string WithCodexHints(
        string? systemPrompt,
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages)
    {
        var hints = BuildHints(workspacePath, packages);
        if (hints.Count == 0)
        {
            return systemPrompt ?? string.Empty;
        }

        var prompt = systemPrompt ?? string.Empty;
        var joined = string.Join("\n\n", hints);
        return string.IsNullOrWhiteSpace(prompt)
            ? joined
            : $"{prompt.TrimEnd()}\n\n{joined}";
    }

    public static async Task<IReadOnlyList<string>> WriteOpencodeHintsAsync(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct)
    {
        var files = new List<string>();
        foreach (var package in packages)
        {
            switch (package)
            {
                case ReviewArtifactSessionSkillPackage review:
                    files.Add(await ReviewArtifactSkillFiles.WriteOpencodeHintAsync(workspacePath, review, ct));
                    break;
                case IntentOperationsSessionSkillPackage intentOps:
                    files.Add(await IntentOperationsSkillFiles.WriteOpencodeHintAsync(workspacePath, intentOps, ct));
                    break;
            }
        }

        return files;
    }

    private static List<string> BuildHints(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages)
    {
        var hints = new List<string>();
        foreach (var package in packages)
        {
            switch (package)
            {
                case ReviewArtifactSessionSkillPackage review:
                    hints.Add(ReviewArtifactSkillFiles.Hint(workspacePath, review));
                    break;
                case IntentOperationsSessionSkillPackage intentOps:
                    hints.Add(IntentOperationsSkillFiles.Hint(workspacePath, intentOps));
                    break;
            }
        }

        return hints;
    }
}
