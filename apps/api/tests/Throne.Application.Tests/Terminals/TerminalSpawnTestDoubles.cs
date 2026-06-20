using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Terminals;

internal static class TerminalSpawnTestDoubles
{
    public static RunPreflightWorkspacePreparer EmptyWorkspacePreparer() =>
        new(Substitute.For<IWorkspaceTrust>(), new WorkspaceAttachmentDumper(EmptyAttachmentRepo()));

    private static IIntentAttachmentRepository EmptyAttachmentRepo()
    {
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentAttachment>>([]));
        return repo;
    }
}
