using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Terminals;

internal static class TerminalSpawnTestDoubles
{
    /// <summary>The production vendor descriptors, wired through the real registry — mirrors the
    /// DI composition so tests exercise the same lookup as the host.</summary>
    public static ITerminalVendorCatalog VendorCatalog() =>
        new TerminalVendorCatalog(
        [
            TerminalVendorDescriptors.Claude,
            TerminalVendorDescriptors.Codex,
            TerminalVendorDescriptors.Opencode,
        ]);

    /// <summary>The production session-skill descriptors, wired through the real registry.</summary>
    public static ISessionSkillCatalog SkillCatalog() =>
        new SessionSkillCatalog(
        [
            SessionSkillDescriptors.Intent,
            SessionSkillDescriptors.Review,
            SessionSkillDescriptors.Dream,
        ]);

    public static RunPreflightWorkspacePreparer EmptyWorkspacePreparer() =>
        new(Substitute.For<IWorkspaceTrust>(), SkillCatalog(), new WorkspaceAttachmentDumper(EmptyAttachmentRepo()));

    private static IIntentAttachmentRepository EmptyAttachmentRepo()
    {
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentAttachment>>([]));
        return repo;
    }
}
