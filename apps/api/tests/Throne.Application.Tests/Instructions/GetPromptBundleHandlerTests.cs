using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.PromptParts;

namespace Throne.Application.Tests.Instructions;

public class GetPromptBundleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions OmitNullJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = "GetPromptBundle для work возвращает system common+work и user common+work в правильном порядке")]
    public async Task Bundle_returns_system_then_user_for_work()
    {
        var repo = SeededRepository();
        var intents = StubIntentRepository();
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetPromptBundleQuery(PromptBundleModeNames.Work, "intent_1"),
            CancellationToken.None);

        bundle.IntentId.Should().Be("intent_1");
        bundle.Mode.Should().Be(PromptBundleModeNames.Work);
        bundle.MissingKeys.Should().BeEmpty();
        bundle.Parts.Select(x => (x.Scope, x.Key)).Should().Equal(
            (PromptPartScopeNames.System, "common"),
            (PromptPartScopeNames.System, "work"),
            (PromptPartScopeNames.User, "common"),
            (PromptPartScopeNames.User, "work"));
        bundle.Parts[0].CurrentVersion.Should().Be(1);
        await intents.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.Work,
            null,
            null,
            IntentTrainingAuthor.System,
            "get_prompt_bundle:work",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GetPromptBundle для interview без user-частей помечает их как missing")]
    public async Task Bundle_reports_missing_user_parts()
    {
        var repo = SeededRepository(includeUser: false);
        var intents = StubIntentRepository();
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetPromptBundleQuery(PromptBundleModeNames.Interview, "intent_1"),
            CancellationToken.None);

        bundle.Parts.Select(x => (x.Scope, x.Key)).Should().Equal(
            (PromptPartScopeNames.System, "common"),
            (PromptPartScopeNames.System, "interview"));
        bundle.MissingKeys.Should().Equal("common", "interview");
    }

    [Fact(DisplayName = "GetPromptBundle для dream возвращает common и dream без смены статуса intent")]
    public async Task Bundle_returns_dream_keys()
    {
        var repo = SeededRepository();
        var intents = StubIntentRepository();
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetPromptBundleQuery(PromptBundleModeNames.Dream, IntentId: null),
            CancellationToken.None);

        bundle.Mode.Should().Be(PromptBundleModeNames.Dream);
        bundle.Parts.Select(x => x.Key).Should().Equal("common", "dream", "common", "dream");
        await intents.DidNotReceiveWithAnyArgs().SetStatusAsync(
            default!, default!, default, default, default, default!, default, default);
    }

    [Fact(DisplayName = "GetPromptBundle отклоняет неизвестный mode")]
    public async Task Bundle_rejects_unknown_mode()
    {
        var handler = NewHandler(SeededRepository(), StubIntentRepository());

        var act = () => handler.HandleAsync(new GetPromptBundleQuery("bad", "intent_1"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Extensions["mode"].Should().Be("bad");
    }

    [Fact(DisplayName = "GetPromptBundle допускает отсутствие intent_id до создания Intent")]
    public async Task Bundle_allows_missing_intent_id()
    {
        var repo = SeededRepository();
        var intents = StubIntentRepository();
        var handler = NewHandler(repo, intents);

        var bundle = await handler.HandleAsync(
            new GetPromptBundleQuery(PromptBundleModeNames.Interview, IntentId: null),
            CancellationToken.None);

        bundle.IntentId.Should().BeNull();
        bundle.Mode.Should().Be(PromptBundleModeNames.Interview);
        await intents.DidNotReceiveWithAnyArgs().SetStatusAsync(
            default!, default!, default, default, default, default!, default, default);
    }

    [Fact(DisplayName = "PromptBundle сериализует intent_id даже когда null (MCP output schema)")]
    public void Bundle_json_includes_null_intent_id_when_omitting_nulls()
    {
        var bundle = new PromptBundle(
            PromptBundleModeNames.Interview,
            IntentId: null,
            Parts: [],
            MissingKeys: []);

        var json = JsonSerializer.Serialize(bundle, OmitNullJsonOptions);

        json.Should().Contain("\"intent_id\":null");
    }

    private static IPromptPartRepository SeededRepository(bool includeUser = true)
    {
        var seeded = new List<PromptPart>
        {
            Part(PromptPartScopeNames.System, "common"),
            Part(PromptPartScopeNames.System, "interview"),
            Part(PromptPartScopeNames.System, "work"),
            Part(PromptPartScopeNames.System, "dream"),
            Part(PromptPartScopeNames.System, "schema_map"),
        };
        if (includeUser)
        {
            seeded.Add(Part(PromptPartScopeNames.User, "common"));
            seeded.Add(Part(PromptPartScopeNames.User, "interview"));
            seeded.Add(Part(PromptPartScopeNames.User, "work"));
            seeded.Add(Part(PromptPartScopeNames.User, "dream"));
        }

        var repo = Substitute.For<IPromptPartRepository>();
        repo.GetByScopeKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var scope = call.ArgAt<string>(0);
                var key = call.ArgAt<string>(1);
                return seeded.FirstOrDefault(p =>
                    string.Equals(p.Scope, scope, StringComparison.Ordinal)
                    && string.Equals(p.Key, key, StringComparison.Ordinal));
            });
        return repo;
    }

    private static PromptPart Part(string scope, string key) =>
        PromptPart.Create(PromptPartId.New(), scope, key, $"{scope} text for {key}", null, [], Now);

    private static GetPromptBundleHandler NewHandler(
        IPromptPartRepository repo,
        IIntentRepository intents)
    {
        var manifestProvider = SkillManifestFixtures.Provider();
        var uow = new PassThroughUnitOfWork();
        var clock = new FixedTimeProvider(Now);
        var auto = new IntentStatusAutoTransition(intents, uow, clock);
        return new GetPromptBundleHandler(manifestProvider, auto, new PromptBundleResolver(repo));
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
