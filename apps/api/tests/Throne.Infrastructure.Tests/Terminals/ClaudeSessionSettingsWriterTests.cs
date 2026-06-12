using System.Text.Json;
using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class ClaudeSessionSettingsWriterTests
{
    [Fact(DisplayName = "Пишет per-session settings с Stop-hook на локальный API")]
    public async Task Writes_stop_hook_settings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throne-settings-{Guid.NewGuid():N}");
        var sut = new ClaudeSessionSettingsWriter(new ClaudeSessionHookOptions
        {
            ApiBaseUrl = "http://localhost:5008/",
        });

        var path = await sut.WriteAsync("intent-1", root, CancellationToken.None);

        path.Should().Be(Path.Combine(root, "throne-session.settings.json"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
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
