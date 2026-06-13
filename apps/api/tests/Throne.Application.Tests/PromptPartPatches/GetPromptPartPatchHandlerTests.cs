using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.PromptPartPatches;

public class GetPromptPartPatchHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Get для proposed-патча с base==current не дёргает text-versions (быстрый путь)")]
    public async Task Get_proposed_base_equals_current_short_circuits()
    {
        var patch = MakePatch("p-1", baseVersion: 5);
        var part = MakePart("part-1", "current text", currentVersion: 5);
        var textVersions = Substitute.For<ITextVersionRepository>();
        var handler = NewHandler(patch, part, textVersions);

        var view = await handler.HandleAsync("p-1", CancellationToken.None);

        view.BaseVersionMatchesCurrent.Should().BeTrue();
        view.CurrentPartText.Should().Be("current text");
        view.BasePartText.Should().Be("current text");
        view.CurrentPartVersion.Should().Be(5);
        await textVersions.DidNotReceiveWithAnyArgs().ListByOwnerAsync(default, default!, default);
    }

    [Fact(DisplayName = "Get для applied-патча реплеит историю до base_version и отдаёт реконструированный base_text")]
    public async Task Get_applied_replays_history_to_base_version()
    {
        var patch = MakePatch("p-1", baseVersion: 2);
        var part = MakePart("part-1", currentText: "v3-final", currentVersion: 3);

        var textVersions = Substitute.For<ITextVersionRepository>();
        textVersions.ListByOwnerAsync(TextVersionOwnerKind.PromptPart, "part-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TextVersion>>(new[]
            {
                Snapshot(version: 1, text: "v1-base"),
                Replace(version: 2, oldText: "v1-base", newText: "v2-base"),
                Replace(version: 3, oldText: "v2-base", newText: "v3-final"),
            }));

        var handler = NewHandler(patch, part, textVersions);

        var view = await handler.HandleAsync("p-1", CancellationToken.None);

        view.BaseVersionMatchesCurrent.Should().BeFalse();
        view.CurrentPartText.Should().Be("v3-final");
        view.CurrentPartVersion.Should().Be(3);
        view.BasePartText.Should().Be("v2-base");
        await textVersions.Received(1).ListByOwnerAsync(
            TextVersionOwnerKind.PromptPart, "part-1", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Get для initial-create патча (base_version=0) возвращает пустой base_text без вызова текст-версий")]
    public async Task Get_initial_create_returns_empty_base()
    {
        var patch = MakePatch("p-1", baseVersion: 0);
        var textVersions = Substitute.For<ITextVersionRepository>();
        var handler = NewHandler(patch, part: null, textVersions);

        var view = await handler.HandleAsync("p-1", CancellationToken.None);

        view.BasePartText.Should().BeEmpty();
        view.CurrentPartText.Should().BeEmpty();
        view.CurrentPartVersion.Should().Be(0);
        view.BaseVersionMatchesCurrent.Should().BeFalse();
        await textVersions.DidNotReceiveWithAnyArgs().ListByOwnerAsync(default, default!, default);
    }

    private static GetPromptPartPatchHandler NewHandler(
        PromptPartPatch patch,
        PromptPart? part,
        ITextVersionRepository textVersions)
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        patches.GetAsync(patch.Identity.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPartPatch?>(patch));

        var parts = Substitute.For<IPromptPartRepository>();
        parts.GetByScopeKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(part));

        return new GetPromptPartPatchHandler(
            patches,
            new UserPromptPartLookup(parts),
            textVersions);
    }

    private static PromptPartPatch MakePatch(string id, int baseVersion) =>
        PromptPartPatch.Create(
            id: id,
            targetScope: PromptPartScopeNames.User,
            targetKey: "work",
            patchText: "patch text",
            evidenceCardIds: [],
            rationale: "r",
            baseVersion: baseVersion,
            now: Now);

    private static PromptPart MakePart(string id, string currentText, int currentVersion) =>
        PromptPart.Restore(
            new PromptPartId(id),
            scope: PromptPartScopeNames.User,
            key: "work",
            text: currentText,
            description: null,
            currentVersion: currentVersion,
            modeRoles: [],
            createdAt: Now,
            updatedAt: Now);

    private static TextVersion Snapshot(int version, string text) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.PromptPart,
        OwnerId: "part-1",
        Version: version,
        Kind: TextVersionKind.Create,
        Delta: new TextVersionDelta(Snapshot: text, OldText: null, NewText: null, AfterLine: null, InsertText: null),
        ChangedAt: Now,
        ChangedBy: TextVersionAuthor.User);

    private static TextVersion Replace(int version, string oldText, string newText) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.PromptPart,
        OwnerId: "part-1",
        Version: version,
        Kind: TextVersionKind.Replace,
        Delta: new TextVersionDelta(Snapshot: null, OldText: oldText, NewText: newText, AfterLine: null, InsertText: null),
        ChangedAt: Now,
        ChangedBy: TextVersionAuthor.User);
}
