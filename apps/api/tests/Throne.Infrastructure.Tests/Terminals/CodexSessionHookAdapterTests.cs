using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class CodexSessionHookAdapterTests
{
    [Fact(DisplayName = "Codex: инлайн -c hooks.Stop с curl на локальный API + bypass-hook-trust")]
    public async Task Builds_inline_stop_hook_override()
    {
        var sut = new CodexSessionHookAdapter(new SessionHookOptions
        {
            ApiBaseUrl = "http://localhost:5008/",
        });

        var args = await sut.PrepareSpawnArgsAsync("intent-1", workspacePath: "/unused", CancellationToken.None);

        args.Should().Equal(
            "-c",
            "hooks.Stop=[{hooks=[{type=\"command\",command=\"" +
            "curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/Stop' " +
            "-H 'Content-Type: application/json' -d @-\",timeout=10}]}]",
            "--dangerously-bypass-hook-trust");
    }

    [Fact(DisplayName = "Codex: per-session слой инлайновый — никаких файлов в workspace")]
    public async Task Writes_no_file_into_workspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sut = new CodexSessionHookAdapter(new SessionHookOptions());

        await sut.PrepareSpawnArgsAsync("intent-1", root, CancellationToken.None);

        Directory.GetFileSystemEntries(root).Should().BeEmpty();
    }
}
