using FluentAssertions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Throne.Api.Mcp.Resources;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Tests.Mcp;

public class IntentAttachmentsResourcesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "resources/list возвращает по одной записи на интент с хотя бы одним аттачем")]
    public async Task List_returns_one_resource_per_intent_with_attachments()
    {
        var withAttachments = IntentId.New();
        var withoutAttachments = IntentId.New();

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.ListAsync(Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Intent>
            {
                Intent.Restore(withAttachments, "user-1", "a", IntentStatusNames.Draft, 1, [], Now, Now),
                Intent.Restore(withoutAttachments, "user-1", "b", IntentStatusNames.Draft, 1, [], Now, Now),
            });

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.CountByIntentAsync(withAttachments, Arg.Any<CancellationToken>()).Returns(2);
        attachments.CountByIntentAsync(withoutAttachments, Arg.Any<CancellationToken>()).Returns(0);

        var resources = NewResources(intentRepo, attachments);

        var result = await resources.ListAsync(NewListRequest(), CancellationToken.None);

        result.Resources.Should().HaveCount(1);
        var entry = result.Resources[0];
        entry.Uri.Should().Be($"intent://{withAttachments.Value}/attachments");
        entry.Name.Should().StartWith("Attachments of intent ");
    }

    [Fact(DisplayName = "resources/read отдаёт BlobResourceContents по одному на аттач")]
    public async Task Read_returns_blob_contents_per_attachment()
    {
        var intentId = IntentId.New();
        var image = new IntentAttachment(
            "att-image", "user-1", intentId.Value, "shot.png", "image/png", 100, Now,
            IsCompressed: true, CompressedWidth: 1024, CompressedHeight: 768);

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.ListByIntentAsync(intentId, Arg.Any<CancellationToken>())
            .Returns(new List<IntentAttachment> { image });
        attachments.OpenContentAsync(intentId, "att-image", Arg.Any<CancellationToken>())
            .Returns(_ => new IntentAttachmentContent(image, new MemoryStream([0x89, 0x50, 0x4E, 0x47])));

        var resources = NewResources(intentRepo, attachments);

        var result = await resources.ReadAsync(
            NewReadRequest($"intent://{intentId.Value}/attachments"),
            CancellationToken.None);

        result.Contents.Should().HaveCount(1);
        var blob = result.Contents[0].Should().BeOfType<BlobResourceContents>().Subject;
        blob.Uri.Should().Be($"intent://{intentId.Value}/attachments/att-image");
        blob.MimeType.Should().Be("image/png");
        blob.Blob.Should().Be(Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }

    [Fact(DisplayName = "resources/read бросает intent.not_found на чужой URI scheme")]
    public async Task Read_throws_for_unknown_uri()
    {
        var resources = NewResources(
            Substitute.For<IIntentRepository>(),
            Substitute.For<IIntentAttachmentRepository>());

        var act = () => resources.ReadAsync(
            NewReadRequest("file:///etc/passwd"),
            CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "resources/read бросает intent.not_found, если интента нет")]
    public async Task Read_throws_when_intent_missing()
    {
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns((Intent?)null);

        var resources = NewResources(intentRepo, Substitute.For<IIntentAttachmentRepository>());

        var act = () => resources.ReadAsync(
            NewReadRequest("intent://missing-intent/attachments"),
            CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "resources/read отдаёт не-image аттач в исходном MIME")]
    public async Task Read_serves_non_image_attachment_as_is()
    {
        var intentId = IntentId.New();
        var pdf = new IntentAttachment(
            "att-pdf", "user-1", intentId.Value, "report.pdf", "application/pdf", 256, Now);

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.ListByIntentAsync(intentId, Arg.Any<CancellationToken>())
            .Returns(new List<IntentAttachment> { pdf });
        attachments.OpenContentAsync(intentId, "att-pdf", Arg.Any<CancellationToken>())
            .Returns(_ => new IntentAttachmentContent(pdf, new MemoryStream([1, 2, 3, 4])));

        var resources = NewResources(intentRepo, attachments);

        var result = await resources.ReadAsync(
            NewReadRequest($"intent://{intentId.Value}/attachments"),
            CancellationToken.None);

        var blob = result.Contents.Single().Should().BeOfType<BlobResourceContents>().Subject;
        blob.Uri.Should().Be($"intent://{intentId.Value}/attachments/att-pdf");
        blob.MimeType.Should().Be("application/pdf");
        blob.Blob.Should().Be(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }));
    }

    private static IntentAttachmentsResources NewResources(
        IIntentRepository intentRepo,
        IIntentAttachmentRepository attachments) =>
        new(intentRepo, attachments);

    private static RequestContext<ListResourcesRequestParams> NewListRequest() =>
        new(Substitute.For<IMcpServer>()) { Params = new ListResourcesRequestParams() };

    private static RequestContext<ReadResourceRequestParams> NewReadRequest(string uri) =>
        new(Substitute.For<IMcpServer>()) { Params = new ReadResourceRequestParams { Uri = uri } };
}
