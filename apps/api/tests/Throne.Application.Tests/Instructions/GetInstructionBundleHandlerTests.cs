using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Instructions;

public class GetInstructionBundleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions OmitNullJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = "GetInstructionBundle для work возвращает system common+work и user common+work в правильном порядке")]
    public async Task Bundle_returns_system_then_user_for_work()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var intents = StubIntentRepository();
        var userCommon = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, InstructionKindNames.Common, "user common text", Now);
        var userWork = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, InstructionKindNames.Work, "user work text", Now);
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([userCommon, userWork]);
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Work, "intent_1"),
            CancellationToken.None);

        bundle.IntentId.Should().Be("intent_1");
        bundle.Mode.Should().Be(InstructionBundleModeNames.Work);
        bundle.MissingKinds.Should().BeEmpty();
        bundle.Instructions.Select(x => (x.Scope, x.Kind)).Should().Equal(
            (InstructionScopeNames.System, InstructionKindNames.Common),
            (InstructionScopeNames.System, InstructionKindNames.Work),
            (InstructionScopeNames.User, InstructionKindNames.Common),
            (InstructionScopeNames.User, InstructionKindNames.Work));
        bundle.Instructions[0].InstructionId.Should().Be("system:common");
        bundle.Instructions[0].CurrentVersion.Should().Be(1);
        bundle.Instructions[2].InstructionId.Should().Be(userCommon.Id.Value);
        await intents.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.Work,
            null,
            null,
            IntentTrainingAuthor.System,
            "get_instruction_bundle:work",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GetInstructionBundle для interview без user-инструкций возвращает только system блок")]
    public async Task Bundle_omits_user_section_when_no_user_instructions()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var intents = StubIntentRepository();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Interview, "intent_1"),
            CancellationToken.None);

        bundle.MissingKinds.Should().BeEmpty();
        bundle.Instructions.Select(x => (x.Scope, x.Kind)).Should().Equal(
            (InstructionScopeNames.System, InstructionKindNames.Common),
            (InstructionScopeNames.System, InstructionKindNames.Interview));
    }

    [Fact(DisplayName = "GetInstructionBundle для dream возвращает common и dream без смены статуса intent")]
    public async Task Bundle_returns_dream_kinds()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var intents = StubIntentRepository();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Dream, IntentId: null),
            CancellationToken.None);

        bundle.Mode.Should().Be(InstructionBundleModeNames.Dream);
        bundle.MissingKinds.Should().BeEmpty();
        bundle.Instructions.Select(x => x.Kind).Should().Equal(
            InstructionKindNames.Common,
            InstructionKindNames.Dream);
        await intents.DidNotReceiveWithAnyArgs().SetStatusAsync(
            default!, default!, default, default, default, default!, default, default);
    }

    [Fact(DisplayName = "GetInstructionBundle отклоняет неизвестный mode")]
    public async Task Bundle_rejects_unknown_mode()
    {
        var handler = NewHandler(Substitute.For<IInstructionRepository>(), StubIntentRepository());

        var act = () => handler.HandleAsync(new GetInstructionBundleQuery("bad", "intent_1"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Extensions["mode"].Should().Be("bad");
    }

    [Fact(DisplayName = "GetInstructionBundle допускает отсутствие intent_id до создания Intent")]
    public async Task Bundle_allows_missing_intent_id()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var intents = StubIntentRepository();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Interview, IntentId: null),
            CancellationToken.None);

        bundle.IntentId.Should().BeNull();
        bundle.Mode.Should().Be(InstructionBundleModeNames.Interview);
        await intents.DidNotReceiveWithAnyArgs().SetStatusAsync(
            default!, default!, default, default, default, default!, default, default);
    }

    [Fact(DisplayName = "InstructionBundle сериализует intent_id даже когда null (MCP output schema)")]
    public void Bundle_json_includes_null_intent_id_when_omitting_nulls()
    {
        var bundle = new InstructionBundle(
            InstructionBundleModeNames.Interview,
            IntentId: null,
            Instructions: [],
            MissingKinds: []);

        var json = JsonSerializer.Serialize(bundle, OmitNullJsonOptions);

        json.Should().Contain("\"intent_id\":null");
    }

    private static GetInstructionBundleHandler NewHandler(
        IInstructionRepository instructions,
        IIntentRepository intents)
    {
        var manifestProvider = SkillManifestFixtures.Provider();
        var uow = new PassThroughUnitOfWork();
        var clock = FakeTimeProvider();
        var auto = new IntentStatusAutoTransition(intents, uow, clock);
        var userEntries = new UserBundleEntries(instructions);
        return new GetInstructionBundleHandler(manifestProvider, auto, userEntries);
    }

    private static IIntentRepository StubIntentRepository()
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.SetStatusAsync(
                Arg.Any<IntentId>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var intentId = ci.ArgAt<IntentId>(0);
                var status = ci.ArgAt<string>(1);
                return new SetIntentStatusOutcome.Updated(
                    Intent.Restore(intentId, "x", status, 1, [], Now, Now));
            });

        return repo;
    }

    private static TimeProvider FakeTimeProvider() => new FixedTimeProvider(Now);

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
