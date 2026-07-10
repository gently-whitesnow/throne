using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals.Quotas;

/// <summary>
/// Reads the Claude Code Pro/Max quota through the undocumented
/// <c>GET https://api.anthropic.com/api/oauth/usage</c> — the same endpoint the CLI's
/// own <c>/status</c> uses via its statusline plug-in (see ADR-0054, community source
/// <c>ohugonnot/claude-code-statusline</c>). Access token is read from
/// <c>~/.claude/.credentials.json</c> (Claude CLI's file-fallback storage; macOS Keychain
/// hosts the primary copy but the file exists whenever the operator has signed in via
/// SSH-friendly flow or set <c>CLAUDE_CONFIG_DIR</c>). We never refresh the token —
/// expiry surfaces as HTTP 401 and the base class swallows it.
/// </summary>
internal sealed class ClaudeQuotaAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<ClaudeQuotaAdapter> logger,
    TimeProvider clock)
    : HttpVendorQuotaAdapterBase(logger, clock)
{
    public const string HttpClientName = "vendor-quota-claude";

    private static readonly string CredentialsPath =
        WorkspacePathExpansion.ExpandHome("~/.claude/.credentials.json");

    private static readonly Uri UsageEndpoint =
        new("https://api.anthropic.com/api/oauth/usage");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public override string Vendor => TerminalAgentCatalog.VendorClaude;

    protected override async Task<VendorQuotaSnapshot?> ProbeAsync(CancellationToken ct)
    {
        var token = ReadAccessToken(CredentialsPath);
        if (token is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // The oauth-2025-04-20 beta header is what the CLI passes on every OAuth call; the
        // server rejects requests without it. `anthropic-version` is optional here.
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(ct);
        return Parse(payload);
    }

    // Public for unit tests.
    internal static VendorQuotaSnapshot? Parse(string payload)
    {
        var doc = JsonSerializer.Deserialize<UsageDocument>(payload, JsonOptions);
        if (doc is null)
        {
            return null;
        }

        var fiveHour = MapWindow(doc.FiveHour);
        if (fiveHour is null)
        {
            return null;
        }
        var sevenDay = MapWindow(doc.SevenDay);
        return new VendorQuotaSnapshot(fiveHour, sevenDay, CreditsBalance: null);
    }

    private static VendorQuotaWindow? MapWindow(UsageWindow? window)
    {
        if (window is null || window.UsedPercentage is null)
        {
            return null;
        }
        return new VendorQuotaWindow(
            UsedPercent: ClampPercent(window.UsedPercentage.Value),
            ResetsAt: window.ResetsAt);
    }

    internal static string? ReadAccessToken(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        using var stream = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<CredentialsDocument>(stream, JsonOptions);
        var token = doc?.ClaudeAiOauth?.AccessToken;
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private sealed record CredentialsDocument(
        [property: JsonPropertyName("claudeAiOauth")] OauthSection? ClaudeAiOauth);

    private sealed record OauthSection(
        [property: JsonPropertyName("accessToken")] string? AccessToken);

    private sealed record UsageDocument(
        [property: JsonPropertyName("five_hour")] UsageWindow? FiveHour,
        [property: JsonPropertyName("seven_day")] UsageWindow? SevenDay);

    private sealed record UsageWindow(
        [property: JsonPropertyName("used_percentage")] double? UsedPercentage,
        [property: JsonPropertyName("resets_at")] string? ResetsAt);
}
