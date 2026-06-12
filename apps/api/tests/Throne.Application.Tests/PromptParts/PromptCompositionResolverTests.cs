using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Tests.Instructions;
using Throne.Domain.Instructions;
using Throne.Domain.PromptParts;

namespace Throne.Application.Tests.PromptParts;

public class PromptCompositionResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Resolve(work) проецирует mandatory system+user части, все выбраны")]
    public async Task Work_projects_mandatory_parts()
    {
        var resolver = NewResolver(out _, optionalParts: []);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Work, null, "intent body"), CancellationToken.None);

        composition.Parts.Select(p => (p.Scope, p.Key)).Should().Equal(
            (InstructionScopeNames.System, InstructionKindNames.Common),
            (InstructionScopeNames.System, InstructionKindNames.Work),
            (InstructionScopeNames.User, InstructionKindNames.Common),
            (InstructionScopeNames.User, InstructionKindNames.Work));
        composition.Parts.Should().OnlyContain(p => p.Role == PromptPartRoleNames.Mandatory && p.Selected);
        composition.UserPrompt.Should().Be("intent body");
        composition.SystemPrompt.Should().Contain("system text for work").And.Contain("user work text");
    }

    [Fact(DisplayName = "Resolve(free) не имеет mandatory частей — только опциональные")]
    public async Task Free_has_no_mandatory_parts()
    {
        var optional = OptionalPart("custom", PromptPartModeNames.Free, PromptPartRoleNames.DefaultOn, 0, "free rule");
        var resolver = NewResolver(out _, optionalParts: [optional]);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Free, null, ""), CancellationToken.None);

        composition.Parts.Should().ContainSingle();
        composition.Parts[0].Key.Should().Be("custom");
        composition.Parts[0].Selected.Should().BeTrue();
        composition.SystemPrompt.Should().Be("free rule");
    }

    [Fact(DisplayName = "Resolve по умолчанию выбирает default_on и не выбирает default_off")]
    public async Task Defaults_select_default_on_only()
    {
        var on = OptionalPart("on", PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 0, "on text");
        var off = OptionalPart("off", PromptPartModeNames.Work, PromptPartRoleNames.DefaultOff, 1, "off text");
        var resolver = NewResolver(out _, optionalParts: [on, off]);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Work, null, ""), CancellationToken.None);

        var optional = composition.Parts.Where(p => p.Role != PromptPartRoleNames.Mandatory).ToArray();
        optional.Single(p => p.Key == "on").Selected.Should().BeTrue();
        optional.Single(p => p.Key == "off").Selected.Should().BeFalse();
        composition.SystemPrompt.Should().Contain("on text").And.NotContain("off text");
    }

    [Fact(DisplayName = "Resolve со selected_part_ids переопределяет дефолты: default_off включается, default_on выключается")]
    public async Task Explicit_selection_overrides_defaults()
    {
        var on = OptionalPart("on", PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 0, "on text");
        var off = OptionalPart("off", PromptPartModeNames.Work, PromptPartRoleNames.DefaultOff, 1, "off text");
        var resolver = NewResolver(out _, optionalParts: [on, off]);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Work, [off.Id.Value], ""), CancellationToken.None);

        var optional = composition.Parts.Where(p => p.Role != PromptPartRoleNames.Mandatory).ToArray();
        optional.Single(p => p.Key == "on").Selected.Should().BeFalse();
        optional.Single(p => p.Key == "off").Selected.Should().BeTrue();
        composition.SelectedPartIds.Should().Contain(off.Id.Value).And.NotContain(on.Id.Value);
    }

    [Fact(DisplayName = "Resolve отклоняет не-embedded режим (dream)")]
    public async Task Rejects_non_embedded_mode()
    {
        var resolver = NewResolver(out _, optionalParts: []);

        var act = () => resolver.ResolveAsync(
            new ResolvePromptCompositionQuery("dream", null, ""), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Mandatory-проекция совпадает с MCP-бандлом get_instruction_bundle(work)")]
    public async Task Mandatory_projection_matches_mcp_bundle()
    {
        var resolver = NewResolver(out var instructions, optionalParts: []);
        var bundleHandler = BundleHandler(instructions);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Work, null, ""), CancellationToken.None);
        var bundle = await bundleHandler.HandleAsync(
            new GetInstructionBundleQuery(InstructionBundleModeNames.Work, IntentId: null), CancellationToken.None);

        var mandatory = composition.Parts
            .Where(p => p.Role == PromptPartRoleNames.Mandatory)
            .Select(p => (p.Scope, p.Key, p.PartId, p.Text));
        mandatory.Should().Equal(bundle.Instructions.Select(i => (i.Scope, i.Kind, i.InstructionId, i.Text)));
    }

    private static PromptPart OptionalPart(string key, string mode, string role, int order, string text) =>
        PromptPart.Create(
            PromptPartId.New(), InstructionScopeNames.User, key, text, null,
            [new PromptPartModeRole(mode, role, order)], Now);

    private static PromptCompositionResolver NewResolver(
        out IInstructionRepository instructions,
        IReadOnlyList<PromptPart> optionalParts)
    {
        instructions = Substitute.For<IInstructionRepository>();
        var userCommon = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, InstructionKindNames.Common, "user common text", Now);
        var userWork = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, InstructionKindNames.Work, "user work text", Now);
        instructions.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([userCommon, userWork]);

        var promptParts = Substitute.For<IPromptPartRepository>();
        promptParts.ListAsync(Arg.Any<CancellationToken>()).Returns(optionalParts);

        return new PromptCompositionResolver(
            SkillManifestFixtures.Provider(),
            new UserBundleEntries(instructions),
            promptParts);
    }

    private static GetInstructionBundleHandler BundleHandler(IInstructionRepository instructions)
    {
        var auto = new IntentStatusAutoTransition(
            Substitute.For<IIntentRepository>(),
            new PassThroughUnitOfWork(),
            new FixedTimeProvider(Now));
        return new GetInstructionBundleHandler(SkillManifestFixtures.Provider(), auto, new UserBundleEntries(instructions));
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
