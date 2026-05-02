using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.Instructions;

public class GetInstructionBundleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions OmitNullJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = "GetInstructionBundle возвращает common и mode-specific инструкции с id и version")]
    public async Task Bundle_returns_required_instructions()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var common = Instruction.Create(InstructionId.New(), InstructionKindNames.Common, "common text", Now);
        var light = Instruction.Create(InstructionId.New(), InstructionKindNames.LightWork, "light text", Now);
        repo.GetByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([light, common]);
        var handler = new GetInstructionBundleHandler(repo);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.LightWork, "intent_1"),
            CancellationToken.None);

        bundle.IntentId.Should().Be("intent_1");
        bundle.Mode.Should().Be(InstructionBundleModeNames.LightWork);
        bundle.MissingKinds.Should().BeEmpty();
        bundle.Instructions.Select(x => x.Kind).Should().Equal(
            InstructionKindNames.Common,
            InstructionKindNames.LightWork);
        bundle.Instructions[0].InstructionId.Should().Be(common.Id.Value);
        bundle.Instructions[0].CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "GetInstructionBundle возвращает missing_kinds когда seed-инструкции отсутствуют")]
    public async Task Bundle_reports_missing_kinds()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var common = Instruction.Create(InstructionId.New(), InstructionKindNames.Common, "common text", Now);
        repo.GetByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([common]);
        var handler = new GetInstructionBundleHandler(repo);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Interview, "intent_1"),
            CancellationToken.None);

        bundle.MissingKinds.Should().Equal(InstructionKindNames.Interview);
    }

    [Fact(DisplayName = "GetInstructionBundle для dream возвращает common и dream")]
    public async Task Bundle_returns_dream_kinds()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var common = Instruction.Create(InstructionId.New(), InstructionKindNames.Common, "common text", Now);
        var dream = Instruction.Create(InstructionId.New(), InstructionKindNames.Dream, "dream text", Now);
        repo.GetByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([dream, common]);
        var handler = new GetInstructionBundleHandler(repo);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Dream, IntentId: null),
            CancellationToken.None);

        bundle.Mode.Should().Be(InstructionBundleModeNames.Dream);
        bundle.MissingKinds.Should().BeEmpty();
        bundle.Instructions.Select(x => x.Kind).Should().Equal(
            InstructionKindNames.Common,
            InstructionKindNames.Dream);
    }

    [Fact(DisplayName = "GetInstructionBundle отклоняет неизвестный mode")]
    public async Task Bundle_rejects_unknown_mode()
    {
        var handler = new GetInstructionBundleHandler(Substitute.For<IInstructionRepository>());

        var act = () => handler.HandleAsync(new GetInstructionBundleQuery("bad", "intent_1"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Extensions["mode"].Should().Be("bad");
    }

    [Fact(DisplayName = "GetInstructionBundle допускает отсутствие intent_id до создания Intent")]
    public async Task Bundle_allows_missing_intent_id()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = new GetInstructionBundleHandler(repo);

        var bundle = await handler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Interview, IntentId: null),
            CancellationToken.None);

        bundle.IntentId.Should().BeNull();
        bundle.Mode.Should().Be(InstructionBundleModeNames.Interview);
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
}
