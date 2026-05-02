using FluentAssertions;
using Throne.Application.Instructions.Manifest;

namespace Throne.Architecture.Tests;

public class SkillLauncherParityTests
{
    private static readonly string[] VendorRoots = { ".claude/skills", ".agents/skills" };

    [Fact(DisplayName = "Каждый skill манифеста имеет SKILL.md в .claude/skills и .agents/skills")]
    public void Every_skill_in_manifest_has_files_under_both_vendor_roots()
    {
        var (manifest, repoRoot) = LoadManifestAndRoot();

        foreach (var skill in manifest.Skills)
        {
            foreach (var vendor in VendorRoots)
            {
                var path = Path.Combine(repoRoot, vendor, skill.Name, "SKILL.md");
                File.Exists(path).Should().BeTrue($"manifest declares skill '{skill.Name}', expected {path}");
            }
        }
    }

    [Fact(DisplayName = "SKILL.md файлы байт-в-байт совпадают с манифестом по name/description/launcher_body")]
    public void Skill_md_files_match_manifest()
    {
        var (manifest, repoRoot) = LoadManifestAndRoot();

        foreach (var skill in manifest.Skills)
        {
            foreach (var vendor in VendorRoots)
            {
                var path = Path.Combine(repoRoot, vendor, skill.Name, "SKILL.md");
                File.Exists(path).Should().BeTrue();
                var content = NormalizeNewlines(File.ReadAllText(path));
                var expected = RenderSkillFile(skill);
                content.Should().Be(expected,
                    $"{path} must be a byte-exact projection of the skill manifest entry. " +
                    "Either update the manifest and let the future installer regenerate the files, or sync the files manually.");
            }
        }
    }

    [Fact(DisplayName = "В .claude/skills и .agents/skills нет skill-файлов сверх манифеста")]
    public void No_extra_skill_directories_outside_manifest()
    {
        var (manifest, repoRoot) = LoadManifestAndRoot();
        var manifestNames = manifest.Skills.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var vendor in VendorRoots)
        {
            var dir = new DirectoryInfo(Path.Combine(repoRoot, vendor));
            if (!dir.Exists)
            {
                continue;
            }
            foreach (var sub in dir.EnumerateDirectories())
            {
                manifestNames.Should().Contain(sub.Name,
                    $"unexpected skill directory '{sub.FullName}' is not declared in the skill manifest.");
            }
        }
    }

    private static string RenderSkillFile(SkillDefinition skill)
    {
        var body = NormalizeNewlines(skill.LauncherBody).TrimEnd('\n');
        return $"---\nname: {skill.Name}\ndescription: {skill.Description}\n---\n\n{body}\n";
    }

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static (SkillManifest manifest, string repoRoot) LoadManifestAndRoot()
    {
        // The skill manifest is also published into bin output (see Throne.Api.csproj
        // Content include), so we cannot use it alone as a repo-root marker. We require
        // the .claude/skills directory to disambiguate the repo root from bin folders.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var manifestPath = Path.Combine(dir.FullName, "specs", "manifest", "throne-skills.yaml");
            var skillsDir = Path.Combine(dir.FullName, ".claude", "skills");
            if (File.Exists(manifestPath) && Directory.Exists(skillsDir))
            {
                var manifest = SkillManifestParser.Parse(File.ReadAllText(manifestPath));
                return (manifest, dir.FullName);
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "Cannot locate repo root (specs/manifest/throne-skills.yaml + .claude/skills) walking up from " +
            AppContext.BaseDirectory);
    }
}
