using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.PromptParts;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class PromptPartsStartupTests(MongoFixture mongo)
{
    [Fact(DisplayName = "Host стартует со stale system prompt_part в Mongo и list берёт system из манифеста")]
    public async Task Host_starts_with_stale_system_prompt_part_in_mongo()
    {
        var dbName = $"throne_api_{Guid.NewGuid():N}";
        await InsertStaleSystemPartAsync(dbName);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateScopes = false;
                o.ValidateOnBuild = false;
            });
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mongo:ConnectionString"] = mongo.ConnectionString,
                    ["Mongo:Database"] = dbName,
                });
            });
        });
        using var client = factory.CreateClient();

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

    private async Task InsertStaleSystemPartAsync(string dbName)
    {
        var db = new MongoClient(mongo.ConnectionString).GetDatabase(dbName);
        var promptParts = db.GetCollection<BsonDocument>("prompt_parts");
        await promptParts.InsertOneAsync(new BsonDocument
        {
            ["_id"] = "system:work",
            ["scope"] = "system",
            ["key"] = "work",
            ["text"] = "stale system text",
            ["description"] = BsonNull.Value,
            ["current_version"] = 1,
            ["mode_roles"] = new BsonArray
            {
                new BsonDocument
                {
                    ["mode"] = "schema_map",
                    ["role"] = "mandatory",
                    ["order"] = 0,
                },
            },
            ["created_at"] = DateTime.UtcNow,
            ["updated_at"] = DateTime.UtcNow,
        });
    }
}
