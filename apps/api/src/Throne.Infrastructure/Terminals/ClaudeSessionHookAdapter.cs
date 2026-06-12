using System.Text.Encodings.Web;
using System.Text.Json;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Claude flavour of <see cref="ISessionHookAdapter"/>: writes a per-session settings file in the
/// intent workspace root and points the CLI at it with <c>--settings &lt;file&gt;</c>. The file lives
/// beside the clone (never inside it), so the repo stays free of runtime state.
/// </summary>
public sealed class ClaudeSessionHookAdapter(SessionHookOptions options) : ISessionHookAdapter
{
    private const string SettingsFileName = "throne-session.settings.json";
    private const string StopEvent = "Stop";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public string Vendor => TerminalAgentCatalog.VendorClaude;

    public async Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId, string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        Directory.CreateDirectory(workspacePath);
        var settingsPath = Path.Combine(workspacePath, SettingsFileName);
        await using (var stream = File.Create(settingsPath))
        {
            await JsonSerializer.SerializeAsync(stream, BuildSettings(intentId), JsonOptions, ct);
            await stream.WriteAsync("\n"u8.ToArray(), ct);
        }

        return ["--settings", settingsPath];
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
                                command = TerminalHookCallback.CurlCommand(
                                    options.ApiBaseUrl, intentId, StopEvent),
                                timeout = 10,
                            },
                        },
                    },
                ],
            },
        };
}
