using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

public interface ISessionSkillMaterializer
{
    Task<MaterializationResult> MaterializeAsync(
        string workspacePath,
        string vendor,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct);
}

public sealed record MaterializationResult(
    IReadOnlyList<MaterializedSkill> Skills,
    string? SystemPromptAppendix);

public sealed record MaterializedSkill(
    string SkillId,
    string SkillMarkdown,
    string? SkillFileName,
    IReadOnlyList<string> BinPaths);

internal sealed class SessionSkillMaterializer : ISessionSkillMaterializer
{
    public async Task<MaterializationResult> MaterializeAsync(
        string workspacePath,
        string vendor,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);

        Directory.CreateDirectory(workspacePath);
        var sourceRoot = SourceRoot();
        var skills = new List<MaterializedSkill>();
        var codexAppendix = new List<string>();

        foreach (var package in DistinctPackages(packages))
        {
            var markdown = await ReadSkillAsync(sourceRoot, package.Id, ct);
            var binPaths = CopyBins(sourceRoot, workspacePath, package.Id);
            var skillFileName = await MaterializeVendorSkillAsync(
                workspacePath, vendor, package.Id, markdown, codexAppendix, ct);
            skills.Add(new MaterializedSkill(package.Id, markdown, skillFileName, binPaths));
        }

        var appendix = codexAppendix.Count == 0 ? null : string.Join("\n\n", codexAppendix);
        return new MaterializationResult(skills, appendix);
    }

    private static IEnumerable<SessionSkillPackage> DistinctPackages(
        IReadOnlyList<SessionSkillPackage> packages) =>
        packages
            .GroupBy(package => package.Id, StringComparer.Ordinal)
            .Select(group => group.First());

    private static async Task<string?> MaterializeVendorSkillAsync(
        string workspacePath,
        string vendor,
        string skillId,
        string markdown,
        List<string> codexAppendix,
        CancellationToken ct)
    {
        switch (vendor)
        {
            case TerminalAgentCatalog.VendorClaude:
                var claudeTarget = Path.Combine(
                    workspacePath, ".claude", "skills", skillId, "SKILL.md");
                Directory.CreateDirectory(Path.GetDirectoryName(claudeTarget)!);
                await File.WriteAllTextAsync(claudeTarget, markdown, Encoding.UTF8, ct);
                return null;

            case TerminalAgentCatalog.VendorCodex:
                codexAppendix.Add(markdown.TrimEnd());
                return null;

            case TerminalAgentCatalog.VendorOpencode:
                var fileName = $"throne-session.{skillId}.md";
                await File.WriteAllTextAsync(
                    Path.Combine(workspacePath, fileName), markdown, Encoding.UTF8, ct);
                return fileName;

            default:
                throw new NotSupportedException($"Unsupported session skill vendor '{vendor}'.");
        }
    }

    private static List<string> CopyBins(
        string sourceRoot,
        string workspacePath,
        string skillId)
    {
        var sourceBin = SourcePath(sourceRoot, "skills", skillId, "bin");
        if (!Directory.Exists(sourceBin))
        {
            return [];
        }

        var targetBin = Path.Combine(workspacePath, "skills", skillId, "bin");
        Directory.CreateDirectory(targetBin);
        var targets = new List<string>();
        foreach (var source in Directory.EnumerateFiles(sourceBin).Order(StringComparer.Ordinal))
        {
            var target = Path.Combine(targetBin, Path.GetFileName(source));
            File.Copy(source, target, overwrite: true);
            FileModeHelpers.MakeExecutable(target);
            targets.Add(target);
        }
        return targets;
    }

    private static async Task<string> ReadSkillAsync(
        string sourceRoot,
        string skillId,
        CancellationToken ct) =>
        (await File.ReadAllTextAsync(
            SourcePath(sourceRoot, "skills", skillId, "SKILL.md"),
            Encoding.UTF8,
            ct)).TrimEnd() + "\n";

    private static string SourcePath(string sourceRoot, params string[] parts) =>
        Path.Combine(new[] { sourceRoot }.Concat(parts).ToArray());

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
        HasSkillSource(dir, SessionSkillPackageIds.Intent, "throne-intent")
        && HasSkillSource(dir, SessionSkillPackageIds.Review, "throne-review")
        && HasSkillSource(dir, SessionSkillPackageIds.Dream, "throne-dream");

    private static bool HasSkillSource(string dir, string skillId, string binName) =>
        File.Exists(Path.Combine(dir, "skills", skillId, "SKILL.md"))
        && File.Exists(Path.Combine(dir, "skills", skillId, "bin", binName));
}
