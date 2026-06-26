using FluentAssertions;
using MongoDB.Bson;
using Throne.MigrateMongoSqlite;

namespace Throne.MigrateMongoSqlite.Tests;

public sealed class TableSpecsTests
{
    [Fact]
    public void Intents_apply_legacy_defaults()
    {
        var values = Values("intents", new BsonDocument
        {
            ["_id"] = "intent-1",
            ["text"] = "ship it",
            ["status"] = "",
            ["tag_ids"] = new BsonArray { "tag-1" },
            ["created_at"] = new DateTime(2026, 6, 26, 10, 30, 0, DateTimeKind.Utc),
            ["updated_at"] = new DateTime(2026, 6, 26, 10, 35, 0, DateTimeKind.Utc),
        });

        values["status"].Should().Be("draft");
        values["current_version"].Should().Be(1);
        values["sort_key"].Should().Be("V");
        values["cleanup_local_state_on_done"].Should().Be(true);
        values["tag_ids"].Should().Be("[\"tag-1\"]");
        values["created_at"].Should().Be("2026-06-26T10:30:00.0000000+00:00");
    }

    [Fact]
    public void Json_columns_preserve_snake_case_wire_shape()
    {
        var values = Values("intent_events", new BsonDocument
        {
            ["_id"] = "event-1",
            ["intent_id"] = "intent-1",
            ["kind"] = "text_changed",
            ["text_change"] = new BsonDocument
            {
                ["old_text"] = "old",
                ["new_text"] = "new",
            },
            ["created_at"] = new DateTime(2026, 6, 26, 11, 0, 0, DateTimeKind.Utc),
        });

        values["text_change"].Should().Be("{\"old_text\":\"old\",\"new_text\":\"new\"}");
    }

    [Fact]
    public void Settings_collection_splits_into_terminal_and_capabilities_rows()
    {
        var terminal = new BsonDocument
        {
            ["_id"] = "terminal",
            ["default_vendor"] = "codex",
        };
        var capabilities = new BsonDocument
        {
            ["_id"] = "singleton",
            ["current_version"] = 0,
            ["updated_at"] = new DateTime(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc),
            ["selections"] = new BsonDocument { ["open_in_ide"] = "vscode" },
        };

        var terminalSpec = Spec("terminal_settings");
        var capabilitiesSpec = Spec("capabilities");
        terminalSpec.Filter!(terminal).Should().BeTrue();
        terminalSpec.Filter(capabilities).Should().BeFalse();
        capabilitiesSpec.Filter!(capabilities).Should().BeTrue();

        Values(terminalSpec, terminal)["default_vendor"].Should().Be("codex");
        var capabilityValues = Values(capabilitiesSpec, capabilities);
        capabilityValues["current_version"].Should().Be(1);
        capabilityValues["selections"].Should().Be("{\"open_in_ide\":\"vscode\"}");
    }

    [Fact]
    public void GitHub_rows_materialize_effective_host()
    {
        var values = Values("repositories", new BsonDocument
        {
            ["_id"] = "repo-1",
            ["provider"] = "github",
            ["owner"] = "gently-whitesnow",
            ["repo"] = "throne",
        });

        values["host"].Should().Be("github.com");
    }

    private static Dictionary<string, object?> Values(string targetTable, BsonDocument document) =>
        Values(Spec(targetTable), document);

    private static Dictionary<string, object?> Values(TableSpec spec, BsonDocument document) =>
        spec.ReadValues(document).ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static TableSpec Spec(string targetTable) =>
        TableSpecs.DocumentTables.Single(table => table.TargetTable == targetTable);
}
