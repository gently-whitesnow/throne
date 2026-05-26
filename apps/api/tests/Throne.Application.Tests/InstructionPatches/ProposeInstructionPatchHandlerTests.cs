using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.InstructionPatches;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.InstructionPatches;

public class ProposeInstructionPatchHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Propose без idempotency_key создаёт новый patch и эмитит InstructionPatchProposed")]
    public async Task Propose_without_key_creates_fresh()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        patches.CreateAsync(Arg.Any<InstructionPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreateInstructionPatchOutcome(call.Arg<InstructionPatch>())));

        var handler = NewHandler(patches, currentVersion: 3);

        var result = await handler.HandleAsync(NewCommand(idempotencyKey: null), CancellationToken.None);

        result.Should().NotBeNull();
        await patches.Received(1).CreateAsync(Arg.Any<InstructionPatch>(), null, Arg.Any<CancellationToken>());
        await patches.DidNotReceive().GetByIdempotencyKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Повторный propose с тем же idempotency_key возвращает существующий patch без вставки и без эмита события")]
    public async Task Propose_with_existing_key_returns_existing()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        var existing = MakePatch("p-existing");
        patches.GetByIdempotencyKeyAsync("dream-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<InstructionPatch?>(existing));

        var handler = NewHandler(patches, currentVersion: 3);

        var result = await handler.HandleAsync(NewCommand(idempotencyKey: "dream-1"), CancellationToken.None);

        result.Identity.Id.Should().Be("p-existing");
        await patches.DidNotReceive().CreateAsync(Arg.Any<InstructionPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Idempotency-retry после рассинхрона базы инструкций всё равно возвращает оригинальный patch без 409 needs_rebase")]
    public async Task Propose_with_existing_key_skips_version_check()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        var existing = MakePatch("p-existing");
        patches.GetByIdempotencyKeyAsync("dream-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<InstructionPatch?>(existing));

        // Instruction.current_version уже ушла вперёд (например, патч из ретрая уже apply-нулся).
        var handler = NewHandler(patches, currentVersion: 99);

        var act = async () => await handler.HandleAsync(
            NewCommand(idempotencyKey: "dream-1", baseInstructionVersion: 3),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Idempotency-key длиной >64 символов отвергается с validation_failed")]
    public async Task Propose_rejects_long_key()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        var handler = NewHandler(patches, currentVersion: 3);

        var act = async () => await handler.HandleAsync(
            NewCommand(idempotencyKey: new string('x', 65)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Пустой / whitespace idempotency_key трактуется как null (поведение без ключа)")]
    public async Task Propose_treats_whitespace_key_as_null()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        patches.CreateAsync(Arg.Any<InstructionPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreateInstructionPatchOutcome(call.Arg<InstructionPatch>())));

        var handler = NewHandler(patches, currentVersion: 3);

        await handler.HandleAsync(NewCommand(idempotencyKey: "   "), CancellationToken.None);

        await patches.DidNotReceive().GetByIdempotencyKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await patches.Received(1).CreateAsync(Arg.Any<InstructionPatch>(), null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propose с base_instruction_version=0 разрешён, когда у пользователя ещё нет user-инструкции (первичный патч)")]
    public async Task Propose_allows_base_zero_when_instruction_missing()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        patches.CreateAsync(Arg.Any<InstructionPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreateInstructionPatchOutcome(call.Arg<InstructionPatch>())));
        var handler = NewHandlerWithoutInstruction(patches);

        var result = await handler.HandleAsync(
            NewCommand(idempotencyKey: null, baseInstructionVersion: 0),
            CancellationToken.None);

        result.Identity.BaseInstructionVersion.Should().Be(0);
        await patches.Received(1).CreateAsync(Arg.Any<InstructionPatch>(), null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propose с base_instruction_version=3 на отсутствующей инструкции — 409 needs_rebase (текущая=0)")]
    public async Task Propose_rejects_non_zero_base_when_instruction_missing()
    {
        var patches = Substitute.For<IInstructionPatchRepository>();
        var handler = NewHandlerWithoutInstruction(patches);

        var act = async () => await handler.HandleAsync(
            NewCommand(idempotencyKey: null, baseInstructionVersion: 3),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.InstructionPatchNeedsRebase);
        ex.Which.Extensions["current_instruction_version"].Should().Be(0);
    }

    [Fact(DisplayName = "CreateInstructionPatchOutcome IsExisting=true не эмитит InstructionPatchProposed")]
    public void Outcome_existing_emits_no_events()
    {
        var patch = MakePatch("p-1");
        var fresh = new CreateInstructionPatchOutcome(patch);
        var dedup = new CreateInstructionPatchOutcome(patch, IsExisting: true);

        fresh.Events.Should().HaveCount(1);
        dedup.Events.Should().BeEmpty();
    }

    private static ProposeInstructionPatchCommand NewCommand(
        string? idempotencyKey = null,
        int baseInstructionVersion = 3) =>
        new(
            TargetKind: InstructionKindNames.Work,
            PatchText: "new text",
            EvidenceCardIds: [],
            Rationale: "because",
            BaseInstructionVersion: baseInstructionVersion,
            IdempotencyKey: idempotencyKey);

    private static InstructionPatch MakePatch(string id) =>
        InstructionPatch.Create(
            id: id,
            ownerUserId: "user-1",
            targetKind: InstructionKindNames.Work,
            patchText: "new text",
            evidenceCardIds: [],
            rationale: "because",
            baseInstructionVersion: 3,
            now: Now);

    private static ProposeInstructionPatchHandler NewHandler(
        IInstructionPatchRepository patches,
        int currentVersion)
    {
        var instructions = Substitute.For<IInstructionRepository>();
        var target = Instruction.Restore(
            InstructionId.New(),
            scope: InstructionScopeNames.User,
            userId: "user-1",
            kind: InstructionKindNames.Work,
            text: "current text",
            currentVersion: currentVersion,
            createdAt: Now,
            updatedAt: Now);
        instructions.GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Instruction>>(new[] { target }));

        return new ProposeInstructionPatchHandler(
            patches,
            new UserInstructionLookup(instructions),
            new PassthroughUnitOfWork(),
            new TestCurrentUserAccessor(),
            new FakeTimeProvider(Now));
    }

    private static ProposeInstructionPatchHandler NewHandlerWithoutInstruction(IInstructionPatchRepository patches)
    {
        var instructions = Substitute.For<IInstructionRepository>();
        instructions.GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Instruction>>([]));

        return new ProposeInstructionPatchHandler(
            patches,
            new UserInstructionLookup(instructions),
            new PassthroughUnitOfWork(),
            new TestCurrentUserAccessor(),
            new FakeTimeProvider(Now));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
