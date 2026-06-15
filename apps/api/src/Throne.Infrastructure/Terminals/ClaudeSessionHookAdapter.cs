using System.Text.Encodings.Web;
using System.Text.Json;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Claude flavour of <see cref="ISessionHookAdapter"/>: writes a per-session settings file in the
/// intent workspace root and points the CLI at it with <c>--settings &lt;file&gt;</c>. The assembled
/// rules block is written beside it and referenced with <c>--append-system-prompt-file</c> (a
/// native Claude flag) rather than inlined — a multi-KB <c>--append-system-prompt</c> token would
/// blow past tmux's spawn-argv imsg limit. Both files live beside the clone (never inside it), so
/// the repo stays free of runtime state and the workspace teardown on intent-done reaps them.
/// </summary>
public sealed class ClaudeSessionHookAdapter(SessionHookOptions options) : ISessionHookAdapter
{
    private const string SettingsFileName = "throne-session.settings.json";
    private const string SystemPromptFileName = "throne-session.append-system-prompt.txt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public string Vendor => TerminalAgentCatalog.VendorClaude;

    public async Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId, string workspacePath, string mode, string? systemPrompt, CancellationToken ct)
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

        var args = new List<string> { "--settings", settingsPath };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            var systemPromptPath = Path.Combine(workspacePath, SystemPromptFileName);
            await File.WriteAllTextAsync(systemPromptPath, systemPrompt!, ct);
            args.Add("--append-system-prompt-file");
            args.Add(systemPromptPath);
        }

        return args;
    }

    // Claude's per-session files (settings + system prompt) live inside the intent workspace, so the
    // workspace-folder removal on intent-done reaps them — no out-of-workspace state to clean here.
    public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

    private object BuildSettings(string intentId, string mode) =>
        new
        {
            hooks = TerminalHookEvents.ClaudeBindings.ToDictionary(
                binding => binding.Event,
                binding => new[] { BuildHookGroup(binding, intentId, mode) },
                StringComparer.Ordinal),
        };

    // A Claude hook group is `{ hooks: [...] }`, optionally prefixed with a `matcher` that scopes
    // which instances of the event fire it (e.g. only `permission_prompt` Notifications). The matcher
    // key is omitted entirely when absent so unscoped events keep matching every occurrence.
    private object BuildHookGroup(TerminalHookBinding binding, string intentId, string mode)
    {
        var hooks = new[]
        {
            new
            {
                type = "command",
                command = TerminalHookCallback.CurlCommand(
                    options.ApiBaseUrl, intentId, binding.Event, mode),
                timeout = 10,
            },
        };

        if (binding.Matcher is null)
        {
            return new { hooks };
        }

        return new { matcher = binding.Matcher, hooks };
    }
}
