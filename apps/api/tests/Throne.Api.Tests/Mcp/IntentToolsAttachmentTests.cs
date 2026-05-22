using FluentAssertions;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Throne.Api.Mcp.Tools;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Tests.Mcp;

public class IntentToolsAttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "get_intent возвращает только метаданные вложений, без image-блоков")]
    public async Task GetIntent_returns_only_text_block_and_attachment_metadata()
    {
        var intentId = IntentId.New();
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(IntentFactory.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(new List<IntentAttachment>
            {
                new("att-1", "user-1", intentId.Value, "shot1.png", "image/png", 12345, Now),
                new("att-2", "user-1", intentId.Value, "shot2.png", "image/png", 67890, Now),
            });

        var tagRepo = Substitute.For<ITagRepository>();
        tagRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var tools = NewTools(intentRepo, attachments, tagRepo);

        var result = await tools.GetIntent(intentId.Value, CancellationToken.None);

        result.Wire.IsError.Should().BeFalse();
        result.Wire.Content.Should().HaveCount(1);
        result.Wire.Content[0].Should().BeOfType<TextContentBlock>();
        result.Wire.StructuredContent.Should().BeNull("ADR-0003 §8.1: prompt-like tools null out wire StructuredContent");
        await attachments.DidNotReceiveWithAnyArgs()
            .OpenContentAsync(default!, default!, default);
    }

    private static IntentTools NewTools(
        IIntentRepository intentRepo,
        IIntentAttachmentRepository attachments,
        ITagRepository tagRepo)
    {
        var linkRepo = Substitute.For<IIntentLinkRepository>();
        linkRepo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        return new(
            create: null!,
            get: new GetIntentHandler(intentRepo),
            getInstructionBundle: null!,
            listIntents: null!,
            moveIntentHandler: null!,
            linkRepository: linkRepo,
            attachments: attachments,
            tagRefs: new IntentToolTagRefs(tagRepo));
    }
}
