using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.PromptParts;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class PromptPartsStartupTests(SqliteFixture sqlite)
{
    [Fact(DisplayName = "Host list игнорирует stale persisted system prompt_part и берёт system из манифеста")]
    public async Task Host_ignores_stale_persisted_system_prompt_part()
    {
        await using var database = sqlite.CreateDatabase();
        await using var factory = SqliteTestHost.Create(database);
        using var client = factory.CreateClient();
        await InsertStaleSystemPartAsync(database.DataSource);

        var health = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var list = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/api/v1/prompt-parts", UriKind.Relative));

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        var systemWork = list.EnumerateArray().Single(p =>
            p.GetProperty("scope").GetString() == "system"
            && p.GetProperty("key").GetString() == "work");
        systemWork.GetProperty("id").GetString().Should().Be("system:work");
        systemWork.GetProperty("text_short").GetString().Should().NotBe("stale system text");
    }

    private static async Task InsertStaleSystemPartAsync(string dataSource)
    {
        await using var connection = new SqliteConnection($"Data Source={dataSource}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO prompt_parts
                (id, scope, key, text, description, current_version, mode_roles, created_at, updated_at)
            VALUES
                ($id, $scope, $key, $text, NULL, $version, $modeRoles, $createdAt, $updatedAt);
            """;
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("$id", "system:work");
        command.Parameters.AddWithValue("$scope", "system");
        command.Parameters.AddWithValue("$key", "work");
        command.Parameters.AddWithValue("$text", "stale system text");
        command.Parameters.AddWithValue("$version", 1);
        command.Parameters.AddWithValue(
            "$modeRoles",
            """[{"mode":"schema_map","role":"mandatory","order":0}]""");
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);
        await command.ExecuteNonQueryAsync();
    }
}
