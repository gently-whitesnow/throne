using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Terminals;
using Throne.Infrastructure.Git;
using Throne.Infrastructure.Terminals;
using Xunit;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Drives <see cref="TmuxSessionManager"/> against a real <c>tmux</c> binary on the
/// developer host. The test exits without failing when tmux is missing — CI runners
/// without it still go green. A docker-based fixture was considered and skipped for
/// Slice 2: tmux on macOS/Linux is already a demo precondition in the parent slice.
/// </summary>
[Trait("Category", "Integration")]
public class TmuxSessionManagerIntegrationTests
{
    [Fact(DisplayName = "Spawn → HasSession → KillSession против реального tmux")]
    public async Task End_to_end_session_lifecycle()
    {
        if (!TmuxProbe.IsAvailable())
        {
            return;
        }

        var intentId = $"t03-{Guid.NewGuid():N}";
        var sut = NewManager();

        var spawn = await sut.SpawnAsync(
            new TmuxSpawnRequest(intentId, AppContext.BaseDirectory, "/bin/sh", ["-c", "sleep 30"]),
            CancellationToken.None);

        try
        {
            spawn.SessionName.Should().Be($"throne-{intentId}");
            spawn.IsAlive.Should().BeTrue();

            (await sut.HasSessionAsync(intentId, CancellationToken.None)).Should().BeTrue();

            var listed = await sut.ListThroneSessionsAsync(CancellationToken.None);
            listed.Should().Contain($"throne-{intentId}");
        }
        finally
        {
            await sut.KillSessionAsync(intentId, CancellationToken.None);
        }

        (await sut.HasSessionAsync(intentId, CancellationToken.None)).Should().BeFalse();
    }

    private static TmuxSessionManager NewManager()
    {
        var launcher = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var cli = new TmuxCli(launcher, Options.Create(new TmuxOptions()));
        return new TmuxSessionManager(
            cli, NullLogger<TmuxSessionManager>.Instance, Substitute.For<IDomainEventDispatcher>());
    }
}
