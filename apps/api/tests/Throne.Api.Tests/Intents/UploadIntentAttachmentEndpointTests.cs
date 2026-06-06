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
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Intents;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class UploadIntentAttachmentEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

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

        var bytes = "hello"u8.ToArray();
        var (response, dto) = await UploadAsync(intentId, bytes, "screenshot.png", "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        dto.Should().NotBeNull();
        dto!.IntentId.Should().Be(intentId);
        dto.FileName.Should().Be("screenshot.png");
        dto.ContentType.Should().Be("image/png");
        dto.SizeBytes.Should().Be(bytes.Length);
        dto.Id.Should().NotBeNullOrWhiteSpace();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(intentId).And.Contain(dto.Id);
    }

    [Fact(DisplayName = "GET/DELETE attachment позволяют смотреть список, скачать и удалить файл")]
    public async Task Can_list_download_and_delete_attachment()
    {
        var intentId = await SeedIntentAsync("intent with preview");
        var bytes = "image bytes"u8.ToArray();
        var (_, uploaded) = await UploadAsync(intentId, bytes, "preview.png", "image/png");
        uploaded.Should().NotBeNull();
        var attachment = uploaded!;

        var list = await _client.GetFromJsonAsync<List<IntentAttachmentView>>(
            new Uri($"/api/v1/intents/{intentId}/attachments", UriKind.Relative));
        list.Should().ContainSingle(x => x.Id == attachment.Id);

        var download = await _client.GetAsync(
            new Uri($"/api/v1/intents/{intentId}/attachments/{attachment.Id}/content", UriKind.Relative));
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

        var deleted = await _client.DeleteAsync(
            new Uri($"/api/v1/intents/{intentId}/attachments/{attachment.Id}", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var empty = await _client.GetFromJsonAsync<List<IntentAttachmentView>>(
            new Uri($"/api/v1/intents/{intentId}/attachments", UriKind.Relative));
        empty.Should().BeEmpty();

        var missing = await _client.GetAsync(
            new Uri($"/api/v1/intents/{intentId}/attachments/{attachment.Id}/content", UriKind.Relative));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
            intent.State.Text,
            Now,
            TextVersionAuthor.User);

        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);
        return intent.Id.Value;
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.State.CurrentVersion,
            intent.State.Status,
            intent.State.Status,
            "test:create",
            Now,
            IntentTrainingAuthor.User);

    private async Task<(HttpResponseMessage Response, IntentAttachmentView? Attachment)> UploadAsync(
        string intentId,
        byte[] bytes,
        string fileName,
        string contentType)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        var response = await _client.PostAsync(
            new Uri($"/api/v1/intents/{intentId}/attachments", UriKind.Relative),
            multipart);
        var dto = await response.Content.ReadFromJsonAsync<IntentAttachmentView>();
        return (response, dto);
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
