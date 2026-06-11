using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Drift-gate for the <c>~/.codex/config.toml</c> merge: it must trust the target directory
/// without disturbing the operator's other projects or global codex settings, stay idempotent on
/// re-spawn, and refuse to touch a config it would otherwise corrupt.
/// </summary>
public class CodexTrustDocumentTests
{
    private const string Path = "/Users/x/.throne/workspaces/intents/abc";

    [Fact(DisplayName = "Создаёт projects-таблицу с trust в пустом/отсутствующем файле")]
    public void Seeds_into_empty_document()
    {
        var updated = CodexTrustDocument.WithTrustedWorkspace(null, Path);

        updated.Should().Contain($"[projects.\"{Path}\"]");
        updated.Should().Contain("trust_level = \"trusted\"");
        CodexTrustDocument.WithTrustedWorkspace(updated, Path).Should().BeNull("повторный спавн — no-op");
    }

    [Fact(DisplayName = "Добавляет trust новому проекту, не трогая прочие ключи и таблицы")]
    public void Adds_entry_preserving_other_keys()
    {
        var existing = """
        model = "gpt-5"

        [projects."/other"]
        trust_level = "trusted"
        """;

        var updated = CodexTrustDocument.WithTrustedWorkspace(existing, Path);

        updated.Should().Contain("model = \"gpt-5\"");
        updated.Should().Contain("[projects.\"/other\"]");
        updated.Should().Contain($"[projects.\"{Path}\"]");
        CodexTrustDocument.WithTrustedWorkspace(updated, Path).Should().BeNull();
    }

    [Fact(DisplayName = "Не переписывает файл, когда trust уже выставлен")]
    public void NoOp_when_already_trusted()
    {
        var existing = $$"""
        [projects."{{Path}}"]
        trust_level = "trusted"
        """;

        CodexTrustDocument.WithTrustedWorkspace(existing, Path).Should().BeNull();
    }

    [Fact(DisplayName = "Выставляет trust в существующей, но недоверенной записи проекта")]
    public void Flips_existing_untrusted_entry()
    {
        var existing = $$"""
        [projects."{{Path}}"]
        trust_level = "untrusted"
        """;

        var updated = CodexTrustDocument.WithTrustedWorkspace(existing, Path);

        updated.Should().Contain("trust_level = \"trusted\"");
        updated.Should().NotContain("untrusted");
        CodexTrustDocument.WithTrustedWorkspace(updated, Path).Should().BeNull();
    }

    [Theory(DisplayName = "Отказывается трогать конфиг, который мог бы испортить")]
    [InlineData("= no key here")]
    [InlineData("projects = \"oops-a-string\"")]
    public void Refuses_to_clobber(string existing)
    {
        CodexTrustDocument.WithTrustedWorkspace(existing, Path).Should().BeNull();
    }

    [Fact(DisplayName = "Untrust: удаляет все таблицы под intent-папкой, сохраняя чужие проекты")]
    public void Untrust_removes_tables_under_prefix_preserving_siblings()
    {
        var underA = Path + "/octo__hello";
        var underB = Path + "/octo__world";
        var existing = $$"""
        model = "gpt-5"

        [projects."{{Path}}"]
        trust_level = "trusted"

        [projects."{{underA}}"]
        trust_level = "trusted"

        [projects."{{underB}}"]
        trust_level = "trusted"

        [projects."/Users/x/.throne/workspaces/intents/abc-extra"]
        trust_level = "trusted"

        [projects."/other"]
        trust_level = "trusted"
        """;

        var updated = CodexTrustDocument.WithoutTrustedWorkspacesUnder(existing, Path);

        updated.Should().Contain("model = \"gpt-5\"");
        updated.Should().NotContain($"[projects.\"{Path}\"]");
        updated.Should().NotContain($"[projects.\"{underA}\"]");
        updated.Should().NotContain($"[projects.\"{underB}\"]");
        // A sibling intent dir that merely shares the textual prefix must survive.
        updated.Should().Contain("[projects.\"/Users/x/.throne/workspaces/intents/abc-extra\"]");
        updated.Should().Contain("[projects.\"/other\"]");
    }

    [Fact(DisplayName = "Untrust: no-op, когда под intent-папкой нет таблиц")]
    public void Untrust_noop_when_nothing_matches()
    {
        var existing = """
        [projects."/other"]
        trust_level = "trusted"
        """;

        CodexTrustDocument.WithoutTrustedWorkspacesUnder(existing, Path).Should().BeNull();
    }

    [Theory(DisplayName = "Untrust: no-op на пустом/отсутствующем файле и нечитаемом формате")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("= no key here")]
    public void Untrust_noop_on_empty_or_unrecognized(string? existing)
    {
        CodexTrustDocument.WithoutTrustedWorkspacesUnder(existing, Path).Should().BeNull();
    }
}
