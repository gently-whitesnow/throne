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
}
