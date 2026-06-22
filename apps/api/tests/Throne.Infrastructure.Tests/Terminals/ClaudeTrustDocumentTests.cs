using System.Text.Json.Nodes;
using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Drift-gate for the <c>~/.claude.json</c> merge: it must seed the target directory's
/// Claude project state without disturbing the operator's other projects or settings, and
/// must refuse to touch documents it would otherwise clobber.
/// </summary>
public class ClaudeTrustDocumentTests
{
    private const string Path = "/Users/x/.throne/workspaces/intents/abc";

    [Fact(DisplayName = "Создаёт projects-запись с trust и onboarding в пустом/отсутствующем файле")]
    public void Seeds_into_empty_document()
    {
        var updated = ClaudeTrustDocument.WithTrustedWorkspace(null, Path);

        ProjectState(updated, Path).Should().Be((true, true, 1));
    }

    [Fact(DisplayName = "Добавляет trust и onboarding новому проекту, не трогая существующие")]
    public void Adds_entry_preserving_siblings()
    {
        var existing = """
        { "numStartups": 7, "projects": { "/other": { "lastCost": 1.5 } } }
        """;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, Path);

        var root = JsonNode.Parse(updated!)!.AsObject();
        root["numStartups"]!.GetValue<int>().Should().Be(7);
        root["projects"]!["/other"]!["lastCost"]!.GetValue<double>().Should().Be(1.5);
        ProjectState(updated, Path).Should().Be((true, true, 1));
    }

    [Fact(DisplayName = "Выставляет trust и onboarding в уже существующей записи проекта, сохраняя её поля")]
    public void Flips_flag_on_existing_entry()
    {
        var existing = $$"""
        { "projects": { "{{Path}}": { "lastCost": 2, "hasTrustDialogAccepted": false } } }
        """;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, Path);

        var entry = JsonNode.Parse(updated!)!["projects"]![Path]!;
        entry["lastCost"]!.GetValue<int>().Should().Be(2);
        ProjectState(updated, Path).Should().Be((true, true, 1));
    }

    [Fact(DisplayName = "Добавляет onboarding к проекту, где уже есть trust")]
    public void Seeds_onboarding_when_trust_already_exists()
    {
        var existing = $$"""
        { "projects": { "{{Path}}": { "hasTrustDialogAccepted": true } } }
        """;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, Path);

        ProjectState(updated, Path).Should().Be((true, true, 1));
    }

    [Fact(DisplayName = "Не переписывает файл, когда trust и onboarding уже выставлены")]
    public void NoOp_when_already_seeded()
    {
        var existing = $$"""
        {
          "projects": {
            "{{Path}}": {
              "hasTrustDialogAccepted": true,
              "hasCompletedProjectOnboarding": true,
              "projectOnboardingSeenCount": 1
            }
          }
        }
        """;

        ClaudeTrustDocument.WithTrustedWorkspace(existing, Path).Should().BeNull();
    }

    [Fact(DisplayName = "Не затирает существующий положительный onboarding seen count")]
    public void Keeps_existing_positive_onboarding_seen_count()
    {
        var existing = $$"""
        {
          "projects": {
            "{{Path}}": {
              "hasTrustDialogAccepted": false,
              "hasCompletedProjectOnboarding": false,
              "projectOnboardingSeenCount": 3
            }
          }
        }
        """;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, Path);

        ProjectState(updated, Path).Should().Be((true, true, 3));
    }

    [Theory(DisplayName = "Отказывается трогать документы, которые мог бы испортить")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "projects": "oops-a-string" }""")]
    [InlineData($$"""{ "projects": { "{{Path}}": "oops-a-string" } }""")]
    public void Refuses_to_clobber(string existing)
    {
        ClaudeTrustDocument.WithTrustedWorkspace(existing, Path).Should().BeNull();
    }

    [Fact(DisplayName = "Untrust: удаляет все записи под intent-папкой, сохраняя чужие проекты")]
    public void Untrust_removes_entries_under_prefix_preserving_siblings()
    {
        var underA = Path + "/octo__hello";
        var underB = Path + "/octo__world";
        var existing = $$"""
        {
          "numStartups": 7,
          "projects": {
            "{{Path}}": { "hasTrustDialogAccepted": true },
            "{{underA}}": { "hasTrustDialogAccepted": true },
            "{{underB}}": { "hasTrustDialogAccepted": true },
            "/Users/x/.throne/workspaces/intents/abc-extra": { "hasTrustDialogAccepted": true },
            "/other": { "lastCost": 1.5 }
          }
        }
        """;

        var updated = ClaudeTrustDocument.WithoutTrustedWorkspacesUnder(existing, Path);

        var projects = JsonNode.Parse(updated!)!["projects"]!.AsObject();
        projects.ContainsKey(Path).Should().BeFalse();
        projects.ContainsKey(underA).Should().BeFalse();
        projects.ContainsKey(underB).Should().BeFalse();
        // A sibling intent dir that merely shares the textual prefix must survive.
        projects.ContainsKey("/Users/x/.throne/workspaces/intents/abc-extra").Should().BeTrue();
        projects["/other"]!["lastCost"]!.GetValue<double>().Should().Be(1.5);
    }

    [Fact(DisplayName = "Untrust: no-op, когда под intent-папкой нет записей")]
    public void Untrust_noop_when_nothing_matches()
    {
        var existing = """{ "projects": { "/other": { "hasTrustDialogAccepted": true } } }""";

        ClaudeTrustDocument.WithoutTrustedWorkspacesUnder(existing, Path).Should().BeNull();
    }

    [Theory(DisplayName = "Untrust: no-op на пустом/отсутствующем файле и нечитаемом формате")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("""{ "projects": "oops-a-string" }""")]
    public void Untrust_noop_on_empty_or_unrecognized(string? existing)
    {
        ClaudeTrustDocument.WithoutTrustedWorkspacesUnder(existing, Path).Should().BeNull();
    }

    private static (bool Trust, bool Onboarding, int SeenCount) ProjectState(string? json, string path)
    {
        var entry = JsonNode.Parse(json!)!["projects"]![path]!;
        return (
            entry["hasTrustDialogAccepted"]!.GetValue<bool>(),
            entry["hasCompletedProjectOnboarding"]!.GetValue<bool>(),
            entry["projectOnboardingSeenCount"]!.GetValue<int>());
    }
}
