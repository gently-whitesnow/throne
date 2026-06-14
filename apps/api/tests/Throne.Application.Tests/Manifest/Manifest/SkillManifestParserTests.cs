using FluentAssertions;
using Throne.Application.Manifest;

namespace Throne.Application.Tests.Manifest.Manifest;

public class SkillManifestParserTests
{
    // work is split by contour (ADR-0034), so it appears twice.
    private static readonly string[] ExpectedBundleModes = ["interview", "work", "work", "dream", "schema_map"];

    private const string ValidYaml = """
        version: 1
        system_instructions:
          - kind: common
            text: "common text"
          - kind: work
            text: "work text"
        bundles:
          - mode: work
            includes:
              - { scope: system, kind: common }
              - { scope: system, kind: work }
              - { scope: user, kind: common }
              - { scope: user, kind: work }
        """;

    [Fact(DisplayName = "SkillManifestParser парсит валидный YAML с system/bundles")]
    public void Parses_valid_manifest()
    {
        var manifest = SkillManifestParser.Parse(ValidYaml);

        manifest.Version.Should().Be(1);
        manifest.SystemInstructions.Should().HaveCount(2);
        manifest.SystemInstructions[0].Kind.Should().Be("common");
        manifest.SystemInstructions[0].Text.Should().Be("common text");
        manifest.Bundles.Should().HaveCount(1);
        manifest.Bundles[0].Mode.Should().Be("work");
        manifest.Bundles[0].Includes.Should().HaveCount(4);
    }

    [Fact(DisplayName = "SkillManifestParser отвергает версию ≠ 1")]
    public void Rejects_unsupported_version()
    {
        var yaml = ValidYaml.Replace("version: 1", "version: 2", StringComparison.Ordinal);
        var act = () => SkillManifestParser.Parse(yaml);
        act.Should().Throw<SkillManifestException>().WithMessage("*version*");
    }

    [Fact(DisplayName = "SkillManifestParser допускает произвольный непустой key в system_instructions (whitelist отменён ADR-0036)")]
    public void Allows_arbitrary_non_empty_system_key()
    {
        var yaml = """
            version: 1
            system_instructions:
              - kind: my_custom_part
                text: "x"
            bundles:
              - mode: work
                includes:
                  - { scope: system, kind: my_custom_part }
            """;
        var manifest = SkillManifestParser.Parse(yaml);
        manifest.SystemInstructions.Should().ContainSingle().Which.Kind.Should().Be("my_custom_part");
    }

    [Fact(DisplayName = "SkillManifestParser отвергает пустой key в system_instructions")]
    public void Rejects_empty_system_key()
    {
        var yaml = """
            version: 1
            system_instructions:
              - kind: ""
                text: "x"
            bundles: []
            """;
        var act = () => SkillManifestParser.Parse(yaml);
        act.Should().Throw<SkillManifestException>().WithMessage("*key*");
    }

    [Fact(DisplayName = "SkillManifestParser отвергает bundle, требующий system kind без записи в system_instructions")]
    public void Rejects_bundle_referencing_missing_system_text()
    {
        var yaml = """
            version: 1
            system_instructions:
              - kind: common
                text: "x"
            bundles:
              - mode: work
                includes:
                  - { scope: system, kind: common }
                  - { scope: system, kind: work }
            """;
        var act = () => SkillManifestParser.Parse(yaml);
        act.Should().Throw<SkillManifestException>().WithMessage("*work*system_instructions*");
    }

    [Fact(DisplayName = "Реальный manifest specs/manifest/throne-skills.yaml парсится без ошибок")]
    public void Parses_real_manifest_from_repo()
    {
        var path = ResolveManifestPath();
        var yaml = File.ReadAllText(path);

        var manifest = SkillManifestParser.Parse(yaml);

        manifest.SystemInstructions.Should().HaveCount(7);
        manifest.Bundles.Should().HaveCount(5);
        manifest.Bundles.Select(b => b.Mode).Should().BeEquivalentTo(ExpectedBundleModes);
        manifest.DreamSources.Should().HaveCount(3);
        manifest.DreamSources.Select(s => s.Vendor).Should().BeEquivalentTo("claude-code", "claude-desktop", "codex-cli");
    }

    [Fact(DisplayName = "Реальный manifest разводит work по контурам standalone/embedded с разным финалом")]
    public void Real_manifest_splits_work_by_contour()
    {
        var manifest = SkillManifestParser.Parse(File.ReadAllText(ResolveManifestPath()));

        var workBundles = manifest.Bundles.Where(b => b.Mode == "work").ToArray();
        workBundles.Select(b => b.Contour).Should().BeEquivalentTo("standalone", "embedded");

        var standalone = workBundles.Single(b => b.Contour == "standalone");
        var embedded = workBundles.Single(b => b.Contour == "embedded");
        standalone.Includes.Should().Contain(i => i.Kind == "finale_standalone");
        embedded.Includes.Should().Contain(i => i.Kind == "finale_embedded");
        // The non-work bundles stay contour-neutral.
        manifest.Bundles.Where(b => b.Mode != "work").Should().OnlyContain(b => b.Contour == null);
    }

    [Fact(DisplayName = "SkillManifestParser парсит поле contour на bundle")]
    public void Parses_contour_field()
    {
        var yaml = """
            version: 1
            system_instructions:
              - kind: common
                text: "x"
            bundles:
              - mode: work
                contour: embedded
                includes:
                  - { scope: system, kind: common }
            """;
        var manifest = SkillManifestParser.Parse(yaml);
        manifest.Bundles.Should().ContainSingle().Which.Contour.Should().Be("embedded");
    }

    [Fact(DisplayName = "SkillManifestParser отвергает неизвестный contour")]
    public void Rejects_unknown_contour()
    {
        var yaml = """
            version: 1
            system_instructions:
              - kind: common
                text: "x"
            bundles:
              - mode: work
                contour: bogus
                includes:
                  - { scope: system, kind: common }
            """;
        var act = () => SkillManifestParser.Parse(yaml);
        act.Should().Throw<SkillManifestException>().WithMessage("*contour*bogus*");
    }

    [Fact(DisplayName = "SkillManifestParser допускает один mode в двух контурах, но отвергает дубль (mode, contour)")]
    public void Rejects_duplicate_mode_contour_pair()
    {
        var twoContours = """
            version: 1
            system_instructions:
              - kind: common
                text: "x"
            bundles:
              - mode: work
                contour: standalone
                includes:
                  - { scope: system, kind: common }
              - mode: work
                contour: embedded
                includes:
                  - { scope: system, kind: common }
            """;
        SkillManifestParser.Parse(twoContours).Bundles.Should().HaveCount(2);

        var duplicate = twoContours.Replace("contour: embedded", "contour: standalone", StringComparison.Ordinal);
        var act = () => SkillManifestParser.Parse(duplicate);
        act.Should().Throw<SkillManifestException>().WithMessage("*duplicate mode*work*");
    }

    private static string ResolveManifestPath()
    {
        // The manifest is also published into bin output via Throne.Api.csproj Content,
        // so we anchor on specs/AGENTS.local.md (only present at repo root) to find the source.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var manifestPath = Path.Combine(dir.FullName, "specs", "manifest", "throne-skills.yaml");
            var anchor = Path.Combine(dir.FullName, "specs", "AGENTS.local.md");
            if (File.Exists(manifestPath) && File.Exists(anchor))
            {
                return manifestPath;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "Cannot locate repo-root throne-skills.yaml (looked for specs/manifest/throne-skills.yaml + specs/AGENTS.local.md) walking up from " +
            AppContext.BaseDirectory);
    }
}
