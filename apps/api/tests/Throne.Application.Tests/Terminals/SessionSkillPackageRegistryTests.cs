using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Terminals;

public class SessionSkillPackageRegistryTests
{
    [Theory(DisplayName = "Выбранный intent-ops материализуется для каждого вендора")]
    [InlineData(TerminalAgentCatalog.VendorClaude)]
    [InlineData(TerminalAgentCatalog.VendorCodex)]
    [InlineData(TerminalAgentCatalog.VendorOpencode)]
    public void Selected_intent_operations_resolves_for_every_vendor(string vendor)
    {
        var registry = NewRegistry();
        var packages = registry.Resolve(new SessionSkillPackageResolution(
            "intent-1", vendor, [SessionSkillPackageIds.IntentOperations], ReviewArtifact: null));

        packages.Should().Equal(new IntentOperationsSessionSkillPackage("intent-1"));
    }

    [Fact(DisplayName = "Review-artifact материализуется без привязки к режиму")]
    public void Selected_review_artifact_resolves_without_mode_gate()
    {
        var registry = NewRegistry();
        var target = new ReviewArtifactWriteTarget(
            "binding-1",
            new RepoCoordinate(GitProviderNames.GitHub, "octo", "repo"));
        var packages = registry.Resolve(new SessionSkillPackageResolution(
            "intent-1", TerminalAgentCatalog.VendorClaude, [SessionSkillPackageIds.ReviewArtifact], target));

        packages.Should().Equal(new ReviewArtifactSessionSkillPackage(target));
    }

    [Fact(DisplayName = "Невыбранные скилы не материализуются")]
    public void Unselected_skills_resolve_no_packages()
    {
        var registry = NewRegistry();
        var packages = registry.Resolve(new SessionSkillPackageResolution(
            "intent-1", TerminalAgentCatalog.VendorClaude, [], ReviewArtifact: null));

        packages.Should().BeEmpty();
    }

    private static SessionSkillPackageRegistry NewRegistry() =>
        new(new InMemorySessionSkillCatalog());
}
