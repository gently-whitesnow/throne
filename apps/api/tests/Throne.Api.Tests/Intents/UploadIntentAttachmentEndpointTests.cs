using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Intents;

public sealed class UploadIntentAttachmentEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithReplicaSet().Build();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        var raw = _mongo.GetConnectionString();
        var separator = raw.Contains('?') ? '&' : '?';
        var connectionString = $"{raw}{separator}directConnection=true";
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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact(DisplayName = "POST /api/v1/intents/{id}/attachments без intent возвращает 404")]
    public async Task Returns_404_when_intent_missing()
    {
        var missingId = Guid.NewGuid().ToString("N");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2, 3]), "file", "a.bin");

        var response = await _client.PostAsync(new Uri($"/api/v1/intents/{missingId}/attachments", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/v1/intents/{id}/attachments сохраняет файл и возвращает 201")]
    public async Task Returns_created_with_metadata()
    {
        var intentId = await SeedIntentAsync("intent with files");

        using var multipart = new MultipartFormDataContent();
        var bytes = "hello"u8.ToArray();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(fileContent, "file", "screenshot.png");

        var response = await _client.PostAsync(
            new Uri($"/api/v1/intents/{intentId}/attachments", UriKind.Relative),
            multipart);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<IntentAttachmentView>();
        dto.Should().NotBeNull();
        dto!.IntentId.Should().Be(intentId);
        dto.FileName.Should().Be("screenshot.png");
        dto.ContentType.Should().Be("image/png");
        dto.SizeBytes.Should().Be(bytes.Length);
        dto.Id.Should().NotBeNullOrWhiteSpace();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(intentId).And.Contain(dto.Id);
    }

    [Fact(DisplayName = "POST /api/v1/intents/{id}/attachments отклоняет 11-й файл (422)")]
    public async Task Returns_422_when_attachment_limit_exceeded()
    {
        var intentId = await SeedIntentAsync("many files");

        for (var i = 0; i < IntentAttachmentLimits.MaxPerIntent; i++)
        {
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new ByteArrayContent([(byte)i]), "file", $"f{i}.bin");
            var ok = await _client.PostAsync(
                new Uri($"/api/v1/intents/{intentId}/attachments", UriKind.Relative),
                multipart);
            ok.StatusCode.Should().Be(HttpStatusCode.Created, $"iteration {i}");
        }

        using var last = new MultipartFormDataContent();
        last.Add(new ByteArrayContent([0xff]), "file", "overflow.bin");
        var response = await _client.PostAsync(
            new Uri($"/api/v1/intents/{intentId}/attachments", UriKind.Relative),
            last);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<string> SeedIntentAsync(string text)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var intent = Intent.Create(IntentId.New(), text, null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"),
            TextVersionOwnerKind.Intent,
            intent.Id.Value,
            intent.Text,
            Now,
            TextVersionAuthor.User);

        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, ct), CancellationToken.None);
        return intent.Id.Value;
    }

    private sealed class IntentAttachmentView
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("intent_id")]
        public string IntentId { get; init; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; init; } = string.Empty;

        [JsonPropertyName("content_type")]
        public string ContentType { get; init; } = string.Empty;

        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
