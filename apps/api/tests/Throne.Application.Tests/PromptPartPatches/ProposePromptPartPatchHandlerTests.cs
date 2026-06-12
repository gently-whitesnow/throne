using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;

namespace Throne.Application.Tests.PromptPartPatches;

public class ProposePromptPartPatchHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Propose без idempotency_key создаёт новый patch и эмитит PromptPartPatchProposed")]
    public async Task Propose_without_key_creates_fresh()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        patches.CreateAsync(Arg.Any<PromptPartPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreatePromptPartPatchOutcome(call.Arg<PromptPartPatch>())));

        var handler = NewHandler(patches, currentVersion: 3);

        var result = await handler.HandleAsync(NewCommand(idempotencyKey: null), CancellationToken.None);

        result.Should().NotBeNull();
        await patches.Received(1).CreateAsync(Arg.Any<PromptPartPatch>(), null, Arg.Any<CancellationToken>());
        await patches.DidNotReceive().GetByIdempotencyKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Повторный propose с тем же idempotency_key возвращает существующий patch без вставки")]
    public async Task Propose_with_existing_key_returns_existing()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        var existing = MakePatch("p-existing");
        patches.GetByIdempotencyKeyAsync("dream-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPartPatch?>(existing));

        var handler = NewHandler(patches, currentVersion: 3);

        var result = await handler.HandleAsync(NewCommand(idempotencyKey: "dream-1"), CancellationToken.None);

        result.Identity.Id.Should().Be("p-existing");
        await patches.DidNotReceive().CreateAsync(Arg.Any<PromptPartPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Idempotency-retry после рассинхрона всё равно возвращает оригинальный patch без 409 needs_rebase")]
    public async Task Propose_with_existing_key_skips_version_check()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        var existing = MakePatch("p-existing");
        patches.GetByIdempotencyKeyAsync("dream-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPartPatch?>(existing));

        var handler = NewHandler(patches, currentVersion: 99);

        var act = async () => await handler.HandleAsync(
            NewCommand(idempotencyKey: "dream-1", baseVersion: 3),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Idempotency-key длиной >64 символов отвергается с validation_failed")]
    public async Task Propose_rejects_long_key()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        var handler = NewHandler(patches, currentVersion: 3);

        var act = async () => await handler.HandleAsync(
            NewCommand(idempotencyKey: new string('x', 65)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Пустой / whitespace idempotency_key трактуется как null")]
    public async Task Propose_treats_whitespace_key_as_null()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        patches.CreateAsync(Arg.Any<PromptPartPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreatePromptPartPatchOutcome(call.Arg<PromptPartPatch>())));

        var handler = NewHandler(patches, currentVersion: 3);

        await handler.HandleAsync(NewCommand(idempotencyKey: "   "), CancellationToken.None);

        await patches.DidNotReceive().GetByIdempotencyKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await patches.Received(1).CreateAsync(Arg.Any<PromptPartPatch>(), null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propose с base_version=0 разрешён, когда части ещё нет (первичный патч)")]
    public async Task Propose_allows_base_zero_when_part_missing()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        patches.CreateAsync(Arg.Any<PromptPartPatch>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CreatePromptPartPatchOutcome(call.Arg<PromptPartPatch>())));
        var handler = NewHandlerWithoutPart(patches);

        var result = await handler.HandleAsync(
            NewCommand(idempotencyKey: null, baseVersion: 0),
            CancellationToken.None);

        result.Identity.BaseVersion.Should().Be(0);
        await patches.Received(1).CreateAsync(Arg.Any<PromptPartPatch>(), null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propose с base_version=3 на отсутствующей части — 409 needs_rebase (текущая=0)")]
    public async Task Propose_rejects_non_zero_base_when_part_missing()
    {
        var patches = Substitute.For<IPromptPartPatchRepository>();
        var handler = NewHandlerWithoutPart(patches);

        var act = async () => await handler.HandleAsync(
            NewCommand(idempotencyKey: null, baseVersion: 3),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.PromptPartPatchNeedsRebase);
        ex.Which.Extensions["current_version"].Should().Be(0);
    }

    [Fact(DisplayName = "CreatePromptPartPatchOutcome IsExisting=true не эмитит PromptPartPatchProposed")]
    public void Outcome_existing_emits_no_events()
    {
        var patch = MakePatch("p-1");
        var fresh = new CreatePromptPartPatchOutcome(patch);
        var dedup = new CreatePromptPartPatchOutcome(patch, IsExisting: true);

        fresh.Events.Should().HaveCount(1);
        dedup.Events.Should().BeEmpty();
    }

    private static ProposePromptPartPatchCommand NewCommand(
        string? idempotencyKey = null,
        int baseVersion = 3) =>
        new(
            TargetScope: PromptPartScopeNames.User,
            TargetKey: "work",
            PatchText: "new text",
            EvidenceCardIds: [],
            Rationale: "because",
            BaseVersion: baseVersion,
            IdempotencyKey: idempotencyKey);

    private static PromptPartPatch MakePatch(string id) =>
        PromptPartPatch.Create(
            id: id,
            targetScope: PromptPartScopeNames.User,
            targetKey: "work",
            patchText: "new text",
            evidenceCardIds: [],
            rationale: "because",
            baseVersion: 3,
            now: Now);

    private static ProposePromptPartPatchHandler NewHandler(
        IPromptPartPatchRepository patches,
        int currentVersion)
    {
        var parts = Substitute.For<IPromptPartRepository>();
        var target = PromptPart.Restore(
            PromptPartId.New(),
            scope: PromptPartScopeNames.User,
            key: "work",
            text: "current text",
            description: null,
            currentVersion: currentVersion,
            modeRoles: [],
            createdAt: Now,
            updatedAt: Now);
        parts.GetByScopeKeyAsync(PromptPartScopeNames.User, "work", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPart?>(target));

        return new ProposePromptPartPatchHandler(
            patches,
            new UserPromptPartLookup(parts),
            new PassthroughUnitOfWork(),
            new FakeTimeProvider(Now));
    }

    private static ProposePromptPartPatchHandler NewHandlerWithoutPart(IPromptPartPatchRepository patches)
    {
        var parts = Substitute.For<IPromptPartRepository>();
        parts.GetByScopeKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPart?>(null));

        return new ProposePromptPartPatchHandler(
            patches,
            new UserPromptPartLookup(parts),
            new PassthroughUnitOfWork(),
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
