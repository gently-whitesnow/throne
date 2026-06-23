using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Terminals;

/// <summary>
/// Closing an intent — <c>done</c> or <c>reject</c> — wipes its local state (agent trust entries +
/// workspace folder), gated solely by the intent's <c>CleanupLocalStateOnClose</c> flag and
/// best-effort so a cleanup failure never escapes the event fan-out. <c>fridge</c> is a pause, not a
/// close, so its state survives. The folder is resolved from the workspace root.
/// </summary>
public class IntentLocalStateCleanupOnCloseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";
    private const string Root = "/ws";
    private static readonly string IntentDir = System.IO.Path.Combine(Root, "intents", IntentIdValue);

    [Theory(DisplayName = "close (done|reject) + флаг true: чистит trust обоих вендоров и удаляет папку")]
    [InlineData(IntentStatusNames.Done)]
    [InlineData(IntentStatusNames.Reject)]
    public async Task Cleans_trust_and_folder_on_close_when_flag_set(string status)
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(status, cleanup: true), CancellationToken.None);

        await fixture.Trust.Received(1).RemoveTrustedUnderAsync(IntentDir, Arg.Any<CancellationToken>());
        await fixture.SpawnAdapter.Received(1).CleanupAsync(IntentIdValue, Arg.Any<CancellationToken>());
        await fixture.Directories.Received(1).RemoveAsync(IntentDir, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "close (done|reject) + флаг false: ничего не чистит")]
    [InlineData(IntentStatusNames.Done)]
    [InlineData(IntentStatusNames.Reject)]
    public async Task Skips_cleanup_when_flag_cleared(string status)
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(status, cleanup: false), CancellationToken.None);

        await fixture.Trust.DidNotReceive().RemoveTrustedUnderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.Directories.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "fridge (заморозка) не трогает локальное состояние")]
    public async Task Skips_cleanup_on_fridge_status()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(IntentStatusNames.Fridge, cleanup: true), CancellationToken.None);

        await fixture.Trust.DidNotReceive().RemoveTrustedUnderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.Directories.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Не-status событие игнорируется")]
    public async Task Ignores_other_events()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(
            new IntentCreated(Restore(IntentStatusNames.Done, cleanup: true)),
            CancellationToken.None);

        await fixture.Trust.DidNotReceive().RemoveTrustedUnderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ошибка удаления папки не ломает переход (best-effort), trust всё равно чистится")]
    public async Task Swallows_directory_failure()
    {
        var fixture = new Fixture();
        fixture.Directories
            .RemoveAsync(IntentDir, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("disk busy"));

        var act = async () =>
            await fixture.Handler.HandleAsync(StatusEvent(IntentStatusNames.Done, cleanup: true), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await fixture.Trust.Received(1).RemoveTrustedUnderAsync(IntentDir, Arg.Any<CancellationToken>());
    }

    private static IntentStatusChanged StatusEvent(string status, bool cleanup) =>
        new(Restore(status, cleanup));

    private static Intent Restore(string status, bool cleanup) =>
        Intent.Restore(
            new IntentId(IntentIdValue), "x", status, 1, [], Now, Now, cleanupLocalStateOnDone: cleanup);

    private sealed class Fixture
    {
        public Fixture()
        {
            Trust = Substitute.For<IWorkspaceTrust>();
            Directories = Substitute.For<IWorkspaceDirectoryRemover>();
            var root = Substitute.For<IWorkspaceRootProvider>();
            root.ResolvedRoot.Returns(Root);
            SpawnAdapter = Substitute.For<ISessionHookAdapter>();
            SpawnAdapter.Vendor.Returns(TerminalAgentCatalog.VendorCodex);
            Handler = new IntentLocalStateCleanupOnCloseHandler(
                Trust, Directories, root, [SpawnAdapter],
                NullLogger<IntentLocalStateCleanupOnCloseHandler>.Instance);
        }

        public IWorkspaceTrust Trust { get; }
        public IWorkspaceDirectoryRemover Directories { get; }
        public ISessionHookAdapter SpawnAdapter { get; }
        public IntentLocalStateCleanupOnCloseHandler Handler { get; }
    }
}
