using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class SessionSkillPackageRegistryTests
{
    [Theory(DisplayName = "Interview default resolves intent operations package for every vendor")]
    [InlineData(TerminalAgentCatalog.VendorClaude)]
    [InlineData(TerminalAgentCatalog.VendorCodex)]
    [InlineData(TerminalAgentCatalog.VendorOpencode)]
    public void Interview_resolves_intent_operations(string vendor)
    {
        var packages = SessionSkillPackageRegistry.Resolve(new SessionSkillPackageResolution(
            "intent-1", TerminalRunModes.Interview, vendor, ReviewArtifact: null));

        packages.Should().Equal(new IntentOperationsSessionSkillPackage("intent-1"));
    }

    [Fact(DisplayName = "Review resolves review artifact package only when target exists")]
    public void Review_resolves_review_artifact_when_target_exists()
    {
        var target = new ReviewArtifactWriteTarget("binding-1", 42);
        var packages = SessionSkillPackageRegistry.Resolve(new SessionSkillPackageResolution(
            "intent-1", TerminalRunModes.Review, TerminalAgentCatalog.VendorClaude, target));

        packages.Should().Equal(new ReviewArtifactSessionSkillPackage(target));
    }

    [Fact(DisplayName = "Non-interview/non-review modes resolve no default skill packages")]
    public void Work_resolves_no_default_packages()
    {
        var packages = SessionSkillPackageRegistry.Resolve(new SessionSkillPackageResolution(
            "intent-1", TerminalRunModes.Work, TerminalAgentCatalog.VendorClaude, ReviewArtifact: null));

        packages.Should().BeEmpty();
    }
}
