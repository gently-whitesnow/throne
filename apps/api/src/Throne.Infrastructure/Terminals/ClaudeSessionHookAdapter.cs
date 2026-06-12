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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public string Vendor => TerminalAgentCatalog.VendorClaude;

    public async Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId, string workspacePath, string mode, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        Directory.CreateDirectory(workspacePath);
        var settingsPath = Path.Combine(workspacePath, SettingsFileName);
        await using (var stream = File.Create(settingsPath))
        {
            await JsonSerializer.SerializeAsync(stream, BuildSettings(intentId, mode), JsonOptions, ct);
            await stream.WriteAsync("\n"u8.ToArray(), ct);
        }

        return ["--settings", settingsPath];
    }

    private object BuildSettings(string intentId, string mode) =>
        new
        {
            hooks = TerminalHookEvents.All.ToDictionary(
                hookEvent => hookEvent,
                hookEvent => new object[]
                {
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = TerminalHookCallback.CurlCommand(
                                    options.ApiBaseUrl, intentId, hookEvent, mode),
                                timeout = 10,
                            },
                        },
                    },
                },
                StringComparer.Ordinal),
        };
}
