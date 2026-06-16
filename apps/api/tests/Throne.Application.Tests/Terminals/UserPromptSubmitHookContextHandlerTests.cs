using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Terminals;

public class UserPromptSubmitHookContextHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Аттачи интента отдаются в виде [intent attachments] блока")]
    public async Task Builds_context_block_from_repository()
    {
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListByIntentAsync(Arg.Is<IntentId>(id => id.Value == "intent-1"), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                NewAttachment("att-1", "shot.png", "image/png"),
            });
        var sut = new UserPromptSubmitHookContextHandler(repo);

        var context = await sut.BuildAsync("intent-1", CancellationToken.None);

        context.Should().Be("[intent attachments]\n- id=att-1 kind=image filename=shot.png");
    }

    [Fact(DisplayName = "Если у интента аттачей нет — возвращаем null, блок не инжектится")]
    public async Task Returns_null_when_repository_empty()
    {
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<IntentAttachment>());
        var sut = new UserPromptSubmitHookContextHandler(repo);

        var context = await sut.BuildAsync("intent-1", CancellationToken.None);

        context.Should().BeNull();
    }

    private static IntentAttachment NewAttachment(string id, string fileName, string contentType) =>
        new(id, "intent-1", fileName, contentType, SizeBytes: 100, CreatedAt: Now);
}
