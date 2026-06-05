using System.Text.Json.Nodes;
using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Drift-gate for the <c>~/.claude.json</c> merge: it must trust the target directory without
/// disturbing the operator's other projects or settings, and must refuse to touch documents it
/// would otherwise clobber.
/// </summary>
public class ClaudeTrustDocumentTests
{
    private const string Path = "/Users/x/.throne/workspaces/intents/abc";

    [Fact(DisplayName = "Создаёт projects-запись с trust в пустом/отсутствующем файле")]
    public void Seeds_into_empty_document()
    {
        var updated = ClaudeTrustDocument.WithTrustedWorkspace(null, Path);

        TrustFlag(updated, Path).Should().BeTrue();
    }

    [Fact(DisplayName = "Добавляет trust новому проекту, не трогая существующие")]
    public void Adds_entry_preserving_siblings()
    {
        var existing = """
        { "numStartups": 7, "projects": { "/other": { "lastCost": 1.5 } } }
        """;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, Path);

        var root = JsonNode.Parse(updated!)!.AsObject();
        root["numStartups"]!.GetValue<int>().Should().Be(7);
        root["projects"]!["/other"]!["lastCost"]!.GetValue<double>().Should().Be(1.5);
        TrustFlag(updated, Path).Should().BeTrue();
    }

    [Fact(DisplayName = "Выставляет trust в уже существующей записи проекта, сохраняя её поля")]
    public void Flips_flag_on_existing_entry()
    {
        var existing = $$"""
        { "projects": { "{{Path}}": { "lastCost": 2, "hasTrustDialogAccepted": false } } }
        """;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, Path);

        var entry = JsonNode.Parse(updated!)!["projects"]![Path]!;
        entry["lastCost"]!.GetValue<int>().Should().Be(2);
        entry["hasTrustDialogAccepted"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "Не переписывает файл, когда trust уже выставлен")]
    public void NoOp_when_already_trusted()
    {
        var existing = $$"""
        { "projects": { "{{Path}}": { "hasTrustDialogAccepted": true } } }
        """;

        ClaudeTrustDocument.WithTrustedWorkspace(existing, Path).Should().BeNull();
    }

    [Theory(DisplayName = "Отказывается трогать документы, которые мог бы испортить")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "projects": "oops-a-string" }""")]
    public void Refuses_to_clobber(string existing)
    {
        ClaudeTrustDocument.WithTrustedWorkspace(existing, Path).Should().BeNull();
    }

    private static bool TrustFlag(string? json, string path) =>
        JsonNode.Parse(json!)!["projects"]![path]!["hasTrustDialogAccepted"]!.GetValue<bool>();
}
