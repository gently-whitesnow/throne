using FluentAssertions;
using Throne.Application.Manifest;
using Throne.Domain.PromptParts;

namespace Throne.Application.Tests.Manifest.Manifest;

public class UserPromptSeedParserTests
{
    private const string ValidYaml = """
        version: 1
        seed_parts:
          - key: common
            description: общая часть
            mode_roles:
              - { mode: work, role: mandatory, order: 1 }
              - { mode: dream, role: mandatory, order: 1 }
            text: |
              common text
          - key: commit
            mode_roles:
              - { mode: work, role: default_off, order: 11 }
            text: |
              commit example
        """;

    [Fact(DisplayName = "UserPromptSeedParser парсит ключи, текст, description и mode_roles")]
    public void Parses_valid_seed()
    {
        var seed = UserPromptSeedParser.Parse(ValidYaml);

        seed.Version.Should().Be(1);
        seed.Parts.Select(p => p.Key).Should().Equal("common", "commit");

        var common = seed.Parts[0];
        common.Description.Should().Be("общая часть");
        common.Text.Trim().Should().Be("common text");
        common.ModeRoles.Should().BeEquivalentTo(new[]
        {
            new PromptPartModeRole("work", PromptPartRoleNames.Mandatory, 1),
            new PromptPartModeRole("dream", PromptPartRoleNames.Mandatory, 1),
        });

        var commit = seed.Parts[1];
        commit.Description.Should().BeNull();
        commit.ModeRoles.Single().Role.Should().Be(PromptPartRoleNames.DefaultOff);
    }

    [Fact(DisplayName = "Дубликат ключа отвергается")]
    public void Rejects_duplicate_key()
    {
        var yaml = """
            version: 1
            seed_parts:
              - key: work
                mode_roles: [{ mode: work, role: mandatory, order: 1 }]
                text: a
              - key: work
                mode_roles: [{ mode: work, role: mandatory, order: 2 }]
                text: b
            """;

        var act = () => UserPromptSeedParser.Parse(yaml);

        act.Should().Throw<SkillManifestException>().WithMessage("*duplicate key 'work'*");
    }

    [Fact(DisplayName = "Неизвестная роль в mode_roles отвергается через доменные инварианты")]
    public void Rejects_unknown_role()
    {
        var yaml = """
            version: 1
            seed_parts:
              - key: work
                mode_roles: [{ mode: work, role: bogus, order: 1 }]
                text: a
            """;

        var act = () => UserPromptSeedParser.Parse(yaml);

        act.Should().Throw<SkillManifestException>().WithMessage("*invalid mode_roles*");
    }

    [Fact(DisplayName = "Пустой текст части отвергается")]
    public void Rejects_empty_text()
    {
        var yaml = """
            version: 1
            seed_parts:
              - key: work
                mode_roles: [{ mode: work, role: mandatory, order: 1 }]
                text: "  "
            """;

        var act = () => UserPromptSeedParser.Parse(yaml);

        act.Should().Throw<SkillManifestException>().WithMessage("*empty text*");
    }
}
