using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class SessionSkillWorkspaceFiles
{
    public static Task WriteScriptsAsync(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct)
    {
        foreach (var scriptName in packages.SelectMany(ScriptsFor).Distinct(StringComparer.Ordinal))
        {
            var target = Path.Combine(workspacePath, "bin", scriptName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(SourcePath("bin", scriptName), target, overwrite: true);
            FileModeHelpers.MakeExecutable(target);
        }
        return Task.CompletedTask;
    }

    public static async Task WriteClaudeSkillsAsync(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct)
    {
        foreach (var package in packages)
        {
            var target = Path.Combine(workspacePath, ".claude", "skills", package.Id, "SKILL.md");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, await ReadSkillAsync(package.Id, ct), Encoding.UTF8, ct);
        }
    }

    public static string WithCodexHints(
        string? systemPrompt,
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages)
    {
        var hints = BuildHints(packages);
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
            var fileName = $"throne-session.{package.Id}.md";
            await File.WriteAllTextAsync(
                Path.Combine(workspacePath, fileName),
                await ReadSkillAsync(package.Id, ct),
                Encoding.UTF8,
                ct);
            files.Add(fileName);
        }
        return files;
    }

    private static List<string> BuildHints(IReadOnlyList<SessionSkillPackage> packages)
    {
        var hints = new List<string>();
        foreach (var package in packages)
        {
            var text = File.ReadAllText(SourcePath("skills", package.Id, "SKILL.md"));
            hints.Add(text.TrimEnd());
        }
        return hints;
    }

    private static async Task<string> ReadSkillAsync(string skillId, CancellationToken ct) =>
        (await File.ReadAllTextAsync(SourcePath("skills", skillId, "SKILL.md"), Encoding.UTF8, ct)).TrimEnd() + "\n";

    private static IReadOnlyList<string> ScriptsFor(SessionSkillPackage package) => package.Id switch
    {
        SessionSkillPackageIds.Intent => ["throne-intent"],
        SessionSkillPackageIds.Review => ["throne-review"],
        SessionSkillPackageIds.Dream => ["throne-dream"],
        _ => [],
    };

    private static string SourcePath(params string[] parts) =>
        Path.Combine(new[] { SourceRoot() }.Concat(parts).ToArray());

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (HasSkillSources(dir.FullName))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Cannot find Throne static skill sources from {AppContext.BaseDirectory}.");
    }

    private static bool HasSkillSources(string dir) =>
        File.Exists(Path.Combine(dir, "skills", "intent", "SKILL.md"))
        && File.Exists(Path.Combine(dir, "skills", "review", "SKILL.md"))
        && File.Exists(Path.Combine(dir, "skills", "dream", "SKILL.md"))
        && File.Exists(Path.Combine(dir, "bin", "throne-intent"))
        && File.Exists(Path.Combine(dir, "bin", "throne-review"))
        && File.Exists(Path.Combine(dir, "bin", "throne-dream"));
}
