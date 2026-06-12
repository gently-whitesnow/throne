using System.Text.Json;
using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class ClaudeSessionHookAdapterTests
{
    [Fact(DisplayName = "Пишет per-session settings с Stop-hook и возвращает --settings <файл>")]
    public async Task Writes_stop_hook_settings_and_returns_flag()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionHookAdapter(new SessionHookOptions
        {
            ApiBaseUrl = "http://localhost:5008/",
        });

        var args = await sut.PrepareSpawnArgsAsync("intent-1", root, CancellationToken.None);

        var settingsPath = Path.Combine(root, "throne-session.settings.json");
        args.Should().Equal("--settings", settingsPath);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        var command = document.RootElement
            .GetProperty("hooks")
            .GetProperty("Stop")[0]
            .GetProperty("hooks")[0]
            .GetProperty("command")
            .GetString();
        command.Should().Be(
            "curl -s -X POST 'http://localhost:5008/api/v1/intents/intent-1/terminal/hooks/Stop' " +
            "-H 'Content-Type: application/json' -d @-");
    }
}
