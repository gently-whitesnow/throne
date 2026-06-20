using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Throne.Application.LocalModels;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// OpenCode flavour of <see cref="ISessionHookAdapter"/>. Four concerns, expressed through
/// workspace-local files the OpenCode CLI auto-discovers at startup
/// (it searches the working directory upward to the nearest git root):
/// <list type="bullet">
///   <item>Provider wiring: a single <c>@ai-sdk/openai-compatible</c> provider keyed by
///   <see cref="TerminalAgentCatalog.OpencodeProviderId"/>, pointed at the operator's local
///   endpoint (<c>Throne:LocalModel:BaseUrl</c>) with an explicit <c>models</c> map mirroring
///   the live <c>/v1/models</c> snapshot. The catalog/resolver already validated the chosen
///   model against the same snapshot — the spawn argv just pins it with
///   <c>--model throne-local/&lt;modelId&gt;</c> (wired by the descriptor's
///   <see cref="TerminalVendorDescriptor.BuildBaseArgs"/>).</item>
///   <item>System-context delivery: OpenCode loads <c>instructions</c>-listed files as
///   ambient guidance, so the assembled rules block is written next to the config and
///   referenced from there — no inlining on the spawn argv. (Mirrors why the Claude/Codex
///   adapters route their multi-KB rules through a file: tmux's spawn imsg has a ~16 KB
///   cap.)</item>
///   <item>Lifecycle hooks: OpenCode 1.17.7+ auto-loads project plugins from
///   <c>.opencode/plugins</c>, so the adapter writes a per-session shim that maps OpenCode
///   lifecycle events onto the existing Throne hook endpoint.</item>
/// </list>
/// </summary>
internal sealed class OpencodeSessionHookAdapter(
    LocalModelDiscoveryService localModels,
    SessionHookOptions hookOptions,
    IOpencodeTuiPortAllocator portAllocator,
    IOpencodeTuiClient tuiClient) : ISessionHookAdapter, INativeInitialPromptSubmitter
{
    private const string ConfigFileName = "opencode.json";
    private const string SystemPromptFileName = "throne-session.append-system-prompt.txt";
    private const string SchemaUrl = "https://opencode.ai/config.json";
    private const string OpenAiCompatibleNpm = "@ai-sdk/openai-compatible";
    private const string ProviderDisplayName = "Throne Local";
    private const string TuiHostname = "127.0.0.1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };
    private readonly ConcurrentDictionary<string, Uri> _tuiEndpoints = new(StringComparer.Ordinal);

    public string Vendor => TerminalAgentCatalog.VendorOpencode;

    public string? ReadinessHookEvent => TerminalHookEvents.SessionReady;

    public async Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId,
        string workspacePath,
        string mode,
        string? systemPrompt,
        ReviewArtifactWriteTarget? reviewArtifact,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        Directory.CreateDirectory(workspacePath);

        var discovery = await localModels.DiscoverAsync(ct);
        // Catalog/resolver gates the spawn before we get here, so a non-ready discovery would
        // only happen on a TOCTOU window between resolution and spawn. Writing whatever the
        // probe currently sees is the same behaviour the resolver enforces and keeps the file
        // valid JSON instead of half-empty.
        var baseUrl = discovery.BaseUrl ?? string.Empty;

        await OpencodePluginShim.WriteAsync(
            workspacePath, intentId, mode, NormalizeBaseUrl(hookOptions.ApiBaseUrl), ct);
        if (reviewArtifact is not null)
        {
            await ReviewArtifactWorkspaceFiles.WriteScriptAsync(
                workspacePath, reviewArtifact, hookOptions.ApiBaseUrl, ct);
        }

        var systemPromptPath = await WriteSystemPromptAsync(workspacePath, systemPrompt, ct);
        var reviewHint = await ReviewArtifactWorkspaceFiles.WriteOpencodeHintAsync(
            workspacePath, reviewArtifact, ct);
        var configPath = Path.Combine(workspacePath, ConfigFileName);
        await using (var stream = File.Create(configPath))
        {
            var document = BuildConfig(baseUrl, discovery.Models, systemPromptPath, reviewHint);
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, ct);
            await stream.WriteAsync("\n"u8.ToArray(), ct);
        }

        var port = portAllocator.Allocate();
        var endpoint = new Uri($"http://{TuiHostname}:{port.ToString(CultureInfo.InvariantCulture)}/");
        _tuiEndpoints[intentId] = endpoint;
        return ["--hostname", TuiHostname, "--port", port.ToString(CultureInfo.InvariantCulture)];
    }

    public async Task SubmitInitialPromptAsync(
        string intentId,
        string workspacePath,
        string userPrompt,
        CancellationToken ct)
    {
        if (!_tuiEndpoints.TryGetValue(intentId, out var endpoint))
        {
            throw new InvalidOperationException($"OpenCode TUI endpoint was not prepared for intent '{intentId}'.");
        }

        try
        {
            await tuiClient.SubmitInitialPromptAsync(endpoint, workspacePath, userPrompt, ct);
        }
        finally
        {
            _tuiEndpoints.TryRemove(intentId, out _);
        }
    }

    // OpenCode initial prompt delivery uses the provider-native TUI HTTP API. The glyph predicate is
    // intentionally disabled so only Claude/Codex keep using capture-pane readiness for tmux paste.
    public bool IsTuiReady(string paneSnapshot) => false;

    // Per-session files live inside the workspace (`opencode.json` + the system-prompt file),
    // so the intent-done workspace teardown reaps them — nothing to do here.
    public Task CleanupAsync(string intentId, CancellationToken ct)
    {
        _tuiEndpoints.TryRemove(intentId, out _);
        return Task.CompletedTask;
    }

    private static async Task<string?> WriteSystemPromptAsync(
        string workspacePath, string? systemPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            return null;
        }
        var path = Path.Combine(workspacePath, SystemPromptFileName);
        await File.WriteAllTextAsync(path, systemPrompt!, ct);
        return path;
    }

    private static OpencodeConfigDocument BuildConfig(
        string baseUrl,
        IReadOnlyList<string> modelIds,
        string? systemPromptPath,
        string? reviewHint)
    {
        var models = new Dictionary<string, OpencodeConfigModel>(StringComparer.Ordinal);
        foreach (var id in modelIds)
        {
            models[id] = new OpencodeConfigModel(Name: id);
        }

        var provider = new OpencodeConfigProvider(
            Npm: OpenAiCompatibleNpm,
            Name: ProviderDisplayName,
            Options: new OpencodeConfigProviderOptions(BaseURL: baseUrl),
            Models: models);

        return new OpencodeConfigDocument(
            Schema: SchemaUrl,
            Provider: new Dictionary<string, OpencodeConfigProvider>(StringComparer.Ordinal)
            {
                [TerminalAgentCatalog.OpencodeProviderId] = provider,
            },
            Instructions: InstructionFiles(systemPromptPath, reviewHint));
    }

    private static string[]? InstructionFiles(string? systemPromptPath, string? reviewHint)
    {
        var files = new List<string>();
        if (systemPromptPath is not null)
        {
            files.Add(Path.GetFileName(systemPromptPath));
        }
        if (reviewHint is not null)
        {
            files.Add(reviewHint);
        }
        return files.Count == 0 ? null : files.ToArray();
    }

    private static string NormalizeBaseUrl(string? apiBaseUrl) =>
        string.IsNullOrWhiteSpace(apiBaseUrl)
            ? SessionHookOptions.DefaultApiBaseUrl
            : apiBaseUrl.TrimEnd('/');
}
