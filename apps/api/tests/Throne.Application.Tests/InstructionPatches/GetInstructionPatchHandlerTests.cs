using FluentAssertions;
using NSubstitute;
using Throne.Application.InstructionPatches;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.InstructionPatches;

public class GetInstructionPatchHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Get для proposed-патча с base==current не дёргает text-versions (быстрый путь) и возвращает base_text==current_text")]
    public async Task Get_proposed_base_equals_current_short_circuits()
    {
        var patch = MakePatch("p-1", baseVersion: 5);
        var instr = MakeInstruction("instr-1", "current text", currentVersion: 5);
        var textVersions = Substitute.For<ITextVersionRepository>();
        var handler = NewHandler(patch, instr, textVersions);

        var view = await handler.HandleAsync("p-1", CancellationToken.None);

        view.BaseVersionMatchesCurrent.Should().BeTrue();
        view.CurrentInstructionText.Should().Be("current text");
        view.BaseInstructionText.Should().Be("current text");
        view.CurrentInstructionVersion.Should().Be(5);
        await textVersions.DidNotReceiveWithAnyArgs().ListByOwnerAsync(default, default!, default);
    }

    [Fact(DisplayName = "Get для applied-патча реплеит историю до base_version и отдаёт реконструированный base_text")]
    public async Task Get_applied_replays_history_to_base_version()
    {
        var patch = MakePatch("p-1", baseVersion: 2);
        var instr = MakeInstruction("instr-1", currentText: "v3-final", currentVersion: 3);

        var textVersions = Substitute.For<ITextVersionRepository>();
        textVersions.ListByOwnerAsync(TextVersionOwnerKind.Instruction, "instr-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TextVersion>>(new[]
            {
                Snapshot(version: 1, text: "v1-base"),
                Replace(version: 2, oldText: "v1-base", newText: "v2-base"),
                Replace(version: 3, oldText: "v2-base", newText: "v3-final"),
            }));

        var handler = NewHandler(patch, instr, textVersions);

        var view = await handler.HandleAsync("p-1", CancellationToken.None);

        view.BaseVersionMatchesCurrent.Should().BeFalse();
        view.CurrentInstructionText.Should().Be("v3-final");
        view.CurrentInstructionVersion.Should().Be(3);
        view.BaseInstructionText.Should().Be("v2-base");
        await textVersions.Received(1).ListByOwnerAsync(
            TextVersionOwnerKind.Instruction, "instr-1", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Get для initial-create патча (base_version=0) возвращает пустой base_text без вызова текст-версий")]
    public async Task Get_initial_create_returns_empty_base()
    {
        var patch = MakePatch("p-1", baseVersion: 0);
        var textVersions = Substitute.For<ITextVersionRepository>();
        // Instruction may not yet exist for initial-create patches.
        var handler = NewHandler(patch, instruction: null, textVersions);

        var view = await handler.HandleAsync("p-1", CancellationToken.None);

        view.BaseInstructionText.Should().BeEmpty();
        view.CurrentInstructionText.Should().BeEmpty();
        view.CurrentInstructionVersion.Should().Be(0);
        view.BaseVersionMatchesCurrent.Should().BeFalse();
        await textVersions.DidNotReceiveWithAnyArgs().ListByOwnerAsync(default, default!, default);
    }

    private static GetInstructionPatchHandler NewHandler(
        InstructionPatch patch,
        Instruction? instruction,
        ITextVersionRepository textVersions)
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        patches.GetAsync(patch.Identity.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<InstructionPatch?>(patch));

        var instructions = Substitute.For<IInstructionRepository>();
        instructions.GetUserInstructionsByKindsAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Instruction>>(
                instruction is null ? Array.Empty<Instruction>() : new[] { instruction }));

        return new GetInstructionPatchHandler(
            patches,
            new UserInstructionLookup(instructions),
            textVersions);
    }

    private static InstructionPatch MakePatch(string id, int baseVersion) =>
        InstructionPatch.Create(
            id: id,
            targetKind: InstructionKindNames.Work,
            patchText: "patch text",
            evidenceCardIds: [],
            rationale: "r",
            baseInstructionVersion: baseVersion,
            now: Now);

    private static Instruction MakeInstruction(string id, string currentText, int currentVersion) =>
        Instruction.Restore(
            new InstructionId(id),
            scope: InstructionScopeNames.User,
            kind: InstructionKindNames.Work,
            text: currentText,
            currentVersion: currentVersion,
            createdAt: Now,
            updatedAt: Now);

    private static TextVersion Snapshot(int version, string text) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.Instruction,
        OwnerId: "instr-1",
        Version: version,
        Kind: TextVersionKind.Create,
        Delta: new TextVersionDelta(Snapshot: text, OldText: null, NewText: null, AfterLine: null, InsertText: null),
        ChangedAt: Now,
        ChangedBy: TextVersionAuthor.User);

    private static TextVersion Replace(int version, string oldText, string newText) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.Instruction,
        OwnerId: "instr-1",
        Version: version,
        Kind: TextVersionKind.Replace,
        Delta: new TextVersionDelta(Snapshot: null, OldText: oldText, NewText: newText, AfterLine: null, InsertText: null),
        ChangedAt: Now,
        ChangedBy: TextVersionAuthor.User);
}
