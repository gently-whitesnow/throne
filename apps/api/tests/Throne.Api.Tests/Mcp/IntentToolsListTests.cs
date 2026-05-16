using FluentAssertions;
using NSubstitute;
using Throne.Api.Mcp.Tools;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Api.Tests.Mcp;

public class IntentToolsListTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "list_intents не падает, когда несколько intents делят один тег (regression)")]
    public async Task ListIntents_handles_shared_tags_across_intents()
    {
        var sharedTagId = TagId.New();
        var intentA = IntentFactory.Restore(IntentId.New(), "user-1", "alpha", IntentStatusNames.Draft, 1, [sharedTagId], Now, Now);
        var intentB = IntentFactory.Restore(IntentId.New(), "user-1", "beta", IntentStatusNames.Draft, 1, [sharedTagId], Now, Now);

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.ListPagedAsync(Arg.Any<IntentListSpec>(), Arg.Any<CancellationToken>())
            .Returns(new IntentListPage([intentA, intentB], NextCursor: null));

        var tagRepo = Substitute.For<ITagRepository>();
        tagRepo.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns([TagFactory.Create(sharedTagId, "shared", Now)]);

        var listHandler = new ListIntentsHandler(intentRepo, tagRepo);
        var tools = NewTools(intentRepo, tagRepo, listHandler);

        var result = await tools.ListIntents(
            tag: null, status: null, query: null, sort: null, limit: null, cursor: null,
            cancellationToken: CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(item =>
            item.Tags.Should().ContainSingle().Which.Name.Should().Be("shared"));
        result.NextCursor.Should().BeNull();
    }

    [Fact(DisplayName = "list_intents строит preview из первой непустой строки и обрезает до 200 символов")]
    public async Task ListIntents_builds_preview_from_first_nonempty_line()
    {
        var longLine = new string('a', 250);
        var intent = IntentFactory.Restore(IntentId.New(), "user-1", $"\n\n{longLine}\n", IntentStatusNames.Draft, 1, [], Now, Now);

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.ListPagedAsync(Arg.Any<IntentListSpec>(), Arg.Any<CancellationToken>())
            .Returns(new IntentListPage([intent], NextCursor: null));

        var tagRepo = Substitute.For<ITagRepository>();
        tagRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var listHandler = new ListIntentsHandler(intentRepo, tagRepo);
        var tools = NewTools(intentRepo, tagRepo, listHandler);

        var result = await tools.ListIntents(
            tag: null, status: null, query: null, sort: null, limit: null, cursor: null,
            cancellationToken: CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Preview.Should().HaveLength(200).And.Be(new string('a', 200));
    }

    [Fact(DisplayName = "list_intents бросает ArgumentException с читаемым текстом при невалидном cursor (SDK обёрнет в IsError)")]
    public async Task ListIntents_returns_readable_error_for_bad_cursor()
    {
        var intentRepo = Substitute.For<IIntentRepository>();
        var tagRepo = Substitute.For<ITagRepository>();
        var listHandler = new ListIntentsHandler(intentRepo, tagRepo);
        var tools = NewTools(intentRepo, tagRepo, listHandler);

        var act = () => tools.ListIntents(
            tag: null, status: null, query: null, sort: null, limit: null, cursor: "not-base64!!!",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("base64");
    }

    [Fact(DisplayName = "list_intents бросает ArgumentException с читаемым текстом при неизвестном sort")]
    public async Task ListIntents_returns_readable_error_for_unknown_sort()
    {
        var intentRepo = Substitute.For<IIntentRepository>();
        var tagRepo = Substitute.For<ITagRepository>();
        var listHandler = new ListIntentsHandler(intentRepo, tagRepo);
        var tools = NewTools(intentRepo, tagRepo, listHandler);

        var act = () => tools.ListIntents(
            tag: null, status: null, query: null, sort: "unknown_sort", limit: null, cursor: null,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("Unknown sort");
    }

    private static IntentTools NewTools(
        IIntentRepository intentRepo,
        ITagRepository tagRepo,
        ListIntentsHandler listHandler) =>
        new(
            create: null!,
            get: new GetIntentHandler(intentRepo),
            getInstructionBundle: null!,
            listIntents: listHandler,
            moveIntentHandler: null!,
            linkRepository: Substitute.For<IIntentLinkRepository>(),
            attachments: Substitute.For<IIntentAttachmentRepository>(),
            tagRepository: tagRepo);
}
