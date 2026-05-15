using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Api.Tests.Instructions;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class CreateInstructionEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var connectionString = mongo.ConnectionString;
        var dbName = $"throne_api_{Guid.NewGuid():N}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
                    ["Mongo:ConnectionString"] = connectionString,
                    ["Mongo:Database"] = dbName,
                });
            });
        });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "POST /api/v1/instructions создаёт user-инструкцию заданного kind для текущего пользователя")]
    public async Task Creates_user_instruction()
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/instructions", UriKind.Relative),
            new CreateInstructionRequestBody { Kind = InstructionKindNames.Work, Text = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IInstructionRepository>();
        var saved = await repo.GetUserInstructionsByKindsAsync(
            "local-dev", [InstructionKindNames.Work], CancellationToken.None);
        saved.Should().HaveCount(1);
        saved[0].Text.Should().Be("hello");
        saved[0].CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "POST /api/v1/instructions: повторный create того же kind отдаёт 409")]
    public async Task Returns_409_when_kind_already_exists()
    {
        var first = await _client.PostAsJsonAsync(
            new Uri("/api/v1/instructions", UriKind.Relative),
            new CreateInstructionRequestBody { Kind = InstructionKindNames.Work, Text = "first" });
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync(
            new Uri("/api/v1/instructions", UriKind.Relative),
            new CreateInstructionRequestBody { Kind = InstructionKindNames.Work, Text = "duplicate" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "POST /api/v1/instructions: неизвестный kind отдаёт 422")]
    public async Task Returns_422_for_unknown_kind()
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/instructions", UriKind.Relative),
            new CreateInstructionRequestBody { Kind = "bogus", Text = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private sealed class CreateInstructionRequestBody
    {
        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }
}
