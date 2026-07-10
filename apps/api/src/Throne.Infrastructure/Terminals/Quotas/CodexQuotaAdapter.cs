using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals.Quotas;

/// <summary>
/// Reads the Codex CLI ChatGPT-Plus quota through the undocumented
/// <c>GET https://chatgpt.com/backend-api/wham/usage</c> — the same endpoint the CLI polls
/// every 60s from its TUI (see ADR-0054; source: <c>codex-rs/backend-client/src/client.rs</c>,
/// upstream issue openai/codex#10869). Access token and account id are read from
/// <c>~/.codex/auth.json</c> (default file storage; a Keychain backend exists but the file
/// is the reference layout). No refresh — expiry surfaces as HTTP 401 and the base class
/// swallows it.
/// </summary>
internal sealed class CodexQuotaAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<CodexQuotaAdapter> logger,
    TimeProvider clock)
    : HttpVendorQuotaAdapterBase(logger, clock)
{
    public const string HttpClientName = "vendor-quota-codex";

    private static readonly string AuthPath =
        WorkspacePathExpansion.ExpandHome("~/.codex/auth.json");

    private static readonly Uri UsageEndpoint =
        new("https://chatgpt.com/backend-api/wham/usage");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public override string Vendor => TerminalAgentCatalog.VendorCodex;

    protected override async Task<VendorQuotaSnapshot?> ProbeAsync(CancellationToken ct)
    {
        var creds = ReadCredentials(AuthPath);
        if (creds is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // originator identifies which surface is calling; codex-cli-rs is what the real CLI
        // sends. ChatGPT-Account-Id is required — the CLI carries it on every /wham call.
        request.Headers.TryAddWithoutValidation("originator", "codex_cli_rs");
        if (!string.IsNullOrEmpty(creds.AccountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", creds.AccountId);
        }

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

        var primary = MapWindow(doc.Primary);
        if (primary is null)
        {
            return null;
        }
        var secondary = MapWindow(doc.Secondary);
        return new VendorQuotaSnapshot(
            FiveHour: primary,
            SevenDay: secondary,
            CreditsBalance: doc.Credits?.Balance);
    }

    private static VendorQuotaWindow? MapWindow(RateLimitWindow? window)
    {
        if (window is null || window.UsedPercent is null)
        {
            return null;
        }
        return new VendorQuotaWindow(
            UsedPercent: ClampPercent(window.UsedPercent.Value),
            ResetsAt: MapResetsAt(window.ResetsAt));
    }

    // /wham/usage reports resets_at as a Unix timestamp (seconds). Normalize to ISO 8601 UTC
    // so the wire is single-format regardless of vendor (Claude reports ISO already).
    private static string? MapResetsAt(long? unixSeconds)
    {
        if (unixSeconds is null || unixSeconds.Value <= 0)
        {
            return null;
        }
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value)
            .UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static CodexCredentials? ReadCredentials(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        using var stream = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<AuthDocument>(stream, JsonOptions);
        var token = doc?.Tokens?.AccessToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }
        return new CodexCredentials(token, doc!.Tokens!.AccountId);
    }

    internal sealed record CodexCredentials(string AccessToken, string? AccountId);

    private sealed record AuthDocument(
        [property: JsonPropertyName("tokens")] TokensSection? Tokens);

    private sealed record TokensSection(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("account_id")] string? AccountId);

    private sealed record UsageDocument(
        [property: JsonPropertyName("primary")] RateLimitWindow? Primary,
        [property: JsonPropertyName("secondary")] RateLimitWindow? Secondary,
        [property: JsonPropertyName("credits")] CreditsSection? Credits);

    private sealed record RateLimitWindow(
        [property: JsonPropertyName("used_percent")] double? UsedPercent,
        [property: JsonPropertyName("resets_at")] long? ResetsAt);

    private sealed record CreditsSection(
        [property: JsonPropertyName("balance")] double? Balance);
}
