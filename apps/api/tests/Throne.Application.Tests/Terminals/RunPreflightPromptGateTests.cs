using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Terminals;
using Throne.Application.Tests.Manifest;
using Throne.Domain.Intents;
using Throne.Domain.PromptParts;

namespace Throne.Application.Tests.Terminals;

public class RunPreflightPromptGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-gate-1";

    [Fact(DisplayName = "Неизвестный selected_part_id отклоняется как validation.failed")]
    public async Task Unknown_selected_part_rejected()
    {
        var (gate, _) = NewGate(optionalParts: [OptionalWorkPart("part-x")]);
        var prompt = new TerminalSpawnPrompt("RULES", "TASK", ["does-not-exist"], IntentTextSave: null);

        var act = () => gate.ApplyAsync(NewIntent(), PromptPartModeNames.Work, prompt, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Известный optional-part проходит валидацию без сохранения текста")]
    public async Task Known_selected_part_passes()
    {
        var (gate, intents) = NewGate(optionalParts: [OptionalWorkPart("part-x")]);
        var prompt = new TerminalSpawnPrompt("RULES", "TASK", ["part-x"], IntentTextSave: null);

        await gate.ApplyAsync(NewIntent(), PromptPartModeNames.Work, prompt, CancellationToken.None);

        await intents.DidNotReceive().ReplaceTextAsync(
            Arg.Any<IntentId>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Throne.Domain.TextVersions.TextVersionAuthor>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "IntentTextSave вызывает replace-text перед spawn")]
    public async Task Intent_text_save_persists()
    {
        var (gate, intents) = NewGate(optionalParts: []);
        intents.ReplaceTextAsync(
                Arg.Any<IntentId>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Throne.Domain.TextVersions.TextVersionAuthor>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new ReplaceIntentTextOutcome.Replaced(NewIntent()));
        var prompt = new TerminalSpawnPrompt("RULES", "TASK", null, new IntentTextSave(3, "old body", "new body"));

        await gate.ApplyAsync(NewIntent(), PromptPartModeNames.Work, prompt, CancellationToken.None);

        await intents.Received(1).ReplaceTextAsync(
            Arg.Is<IntentId>(i => i.Value == IntentIdValue), 3, "old body", "new body",
            Arg.Any<Throne.Domain.TextVersions.TextVersionAuthor>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Version conflict при сохранении текста блокирует запуск (intent.version_conflict)")]
    public async Task Intent_text_version_conflict_blocks()
    {
        var (gate, intents) = NewGate(optionalParts: []);
        intents.ReplaceTextAsync(
                Arg.Any<IntentId>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Throne.Domain.TextVersions.TextVersionAuthor>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new ReplaceIntentTextOutcome.VersionConflict(7));
        var prompt = new TerminalSpawnPrompt("RULES", "TASK", null, new IntentTextSave(3, "old body", "new body"));

        var act = () => gate.ApplyAsync(NewIntent(), PromptPartModeNames.Work, prompt, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentVersionConflict);
    }

    private static Intent NewIntent() =>
        Intent.Restore(new IntentId(IntentIdValue), "intent body", IntentStatusNames.Work, 3, [], Now, Now);

    private static PromptPart OptionalWorkPart(string id) =>
        PromptPart.Restore(
            new PromptPartId(id),
            "system",
            id,
            "optional rule",
            description: null,
            currentVersion: 1,
            [new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOff, 0)],
            Now,
            Now);

    private static (RunPreflightPromptGate Gate, IIntentRepository Intents) NewGate(IReadOnlyList<PromptPart> optionalParts)
    {
        var promptParts = Substitute.For<IPromptPartRepository>();
        promptParts.GetByScopeKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => PromptPart.Create(
                PromptPartId.New(), call.ArgAt<string>(0), call.ArgAt<string>(1),
                $"{call.ArgAt<string>(0)} {call.ArgAt<string>(1)}", null, [], Now));
        promptParts.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(optionalParts);
        var resolver = new PromptCompositionResolver(
            SkillManifestFixtures.Provider(), promptParts);

        var intents = Substitute.For<IIntentRepository>();
        var replaceText = new ReplaceIntentTextHandler(intents, new PassthroughUnitOfWork(), new FixedClock(Now));
        return (new RunPreflightPromptGate(resolver, replaceText), intents);
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
