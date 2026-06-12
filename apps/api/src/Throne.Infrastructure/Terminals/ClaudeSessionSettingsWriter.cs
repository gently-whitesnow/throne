using System.Text.Encodings.Web;
using System.Text.Json;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

public sealed class ClaudeSessionSettingsWriter(ClaudeSessionHookOptions options) : IClaudeSessionSettingsWriter
{
    private const string SettingsFileName = "throne-session.settings.json";
    private const string StopEvent = "Stop";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public async Task<string> WriteAsync(string intentId, string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        Directory.CreateDirectory(workspacePath);
        var settingsPath = Path.Combine(workspacePath, SettingsFileName);
        var settings = BuildSettings(intentId);
        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct);
        await stream.WriteAsync("\n"u8.ToArray(), ct);
        return settingsPath;
    }

    private object BuildSettings(string intentId) =>
        new
        {
            hooks = new Dictionary<string, object[]>
            {
                [StopEvent] =
                [
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = BuildCurlCommand(intentId, StopEvent),
                                timeout = 10,
                            },
                        },
                    },
                ],
            },
        };

    private string BuildCurlCommand(string intentId, string hookEvent)
    {
        var baseUrl = options.ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/intents/{Uri.EscapeDataString(intentId)}/terminal/hooks/{hookEvent}";
        return $"curl -s -X POST {ShellQuote(url)} -H 'Content-Type: application/json' -d @-";
    }

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}
