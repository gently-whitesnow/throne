using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Tests.Instructions;
using Throne.Domain.Intents;
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
            (PromptPartScopeNames.System, "common"),
            (PromptPartScopeNames.System, "work"),
            (PromptPartScopeNames.User, "common"),
            (PromptPartScopeNames.User, "work"));
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

    [Fact(DisplayName = "Resolve отклоняет неизвестный режим")]
    public async Task Rejects_unknown_mode()
    {
        var resolver = NewResolver(out _, optionalParts: []);

        var act = () => resolver.ResolveAsync(
            new ResolvePromptCompositionQuery("bogus_mode", null, ""), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Mandatory-проекция совпадает с MCP-бандлом get_prompt_bundle(work)")]
    public async Task Mandatory_projection_matches_mcp_bundle()
    {
        var resolver = NewResolver(out var repository, optionalParts: []);
        var bundleHandler = BundleHandler(repository);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Work, null, ""), CancellationToken.None);
        var bundle = await bundleHandler.HandleAsync(
            new GetPromptBundleQuery(PromptBundleModeNames.Work, IntentId: null), CancellationToken.None);

        var mandatory = composition.Parts
            .Where(p => p.Role == PromptPartRoleNames.Mandatory)
            .Select(p => (p.Scope, p.Key, p.PartId, p.Text));
        mandatory.Should().Equal(bundle.Parts.Select(i => (i.Scope, i.Key, i.PromptPartId, i.Text)));
    }

    private static PromptPart OptionalPart(string key, string mode, string role, int order, string text) =>
        PromptPart.Create(
            PromptPartId.New(), PromptPartScopeNames.User, key, text, null,
            [new PromptPartModeRole(mode, role, order)], Now);

    private static PromptPart SeedPart(string scope, string key, string text) =>
        PromptPart.Create(PromptPartId.New(), scope, key, text, null, [], Now);

    private static IPromptPartRepository BuildRepository(IReadOnlyList<PromptPart> optionalParts)
    {
        // Mandatory parts: every (scope, key) in the manifest bundles materialised in prompt_parts.
        var seeded = new List<PromptPart>
        {
            SeedPart(PromptPartScopeNames.System, "common", "system text for common"),
            SeedPart(PromptPartScopeNames.System, "interview", "system text for interview"),
            SeedPart(PromptPartScopeNames.System, "work", "system text for work"),
            SeedPart(PromptPartScopeNames.System, "dream", "system text for dream"),
            SeedPart(PromptPartScopeNames.System, "schema_map", "system text for schema_map"),
            SeedPart(PromptPartScopeNames.User, "common", "user common text"),
            SeedPart(PromptPartScopeNames.User, "interview", "user interview text"),
            SeedPart(PromptPartScopeNames.User, "work", "user work text"),
            SeedPart(PromptPartScopeNames.User, "dream", "user dream text"),
        };

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
        repo.ListAsync(Arg.Any<CancellationToken>()).Returns(optionalParts);
        return repo;
    }

    private static PromptCompositionResolver NewResolver(
        out IPromptPartRepository repository,
        IReadOnlyList<PromptPart> optionalParts)
    {
        repository = BuildRepository(optionalParts);
        return new PromptCompositionResolver(
            SkillManifestFixtures.Provider(),
            new PromptBundleResolver(repository),
            repository);
    }

    private static GetPromptBundleHandler BundleHandler(IPromptPartRepository repository)
    {
        var auto = new IntentStatusAutoTransition(
            Substitute.For<IIntentRepository>(),
            new PassThroughUnitOfWork(),
            new FixedTimeProvider(Now));
        return new GetPromptBundleHandler(SkillManifestFixtures.Provider(), auto, new PromptBundleResolver(repository));
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
