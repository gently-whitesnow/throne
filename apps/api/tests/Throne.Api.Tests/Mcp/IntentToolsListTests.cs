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
        var intentA = Intent.Restore(IntentId.New(), "user-1", "alpha", IntentStatusNames.Draft, 1, [sharedTagId], Now, Now);
        var intentB = Intent.Restore(IntentId.New(), "user-1", "beta", IntentStatusNames.Draft, 1, [sharedTagId], Now, Now);

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.ListPagedAsync(Arg.Any<IntentListSpec>(), Arg.Any<CancellationToken>())
            .Returns(new IntentListPage([intentA, intentB], NextCursor: null));

        var tagRepo = Substitute.For<ITagRepository>();
        tagRepo.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns([Tag.Create(sharedTagId, "shared", Now)]);

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
        var intent = Intent.Restore(IntentId.New(), "user-1", $"\n\n{longLine}\n", IntentStatusNames.Draft, 1, [], Now, Now);

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

    private static IntentTools NewTools(
        IIntentRepository intentRepo,
        ITagRepository tagRepo,
        ListIntentsHandler listHandler) =>
        new(
            create: null!,
            get: new GetIntentHandler(intentRepo),
            read: null!,
            replace: null!,
            insertAfterLine: null!,
            search: null!,
            addQa: null!,
            addReview: null!,
            setStatus: null!,
            setTagsHandler: null!,
            getInstructionBundle: null!,
            listIntents: listHandler,
            attachments: Substitute.For<IIntentAttachmentRepository>(),
            tagRepository: tagRepo);
}
