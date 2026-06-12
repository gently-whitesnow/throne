namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Builds the shell command an agent's hook runs to call back into Throne's local API
/// (<c>POST /api/v1/intents/{id}/terminal/hooks/{event}</c>). Vendor-neutral — both the Claude
/// settings file and the Codex inline config override embed the same curl. The hook event payload
/// is forwarded as the request body (<c>-d @-</c> reads the hook's stdin JSON).
///
/// The spawn <paramref name="mode"/> is baked into the URL as a query so status derivation stays
/// stateless: the endpoint reads the return phase off the request the hook itself carries, with no
/// server-side session-mode store (ADR-0034 §4).
/// </summary>
internal static class TerminalHookCallback
{
    public static string CurlCommand(string apiBaseUrl, string intentId, string hookEvent, string mode)
    {
        var baseUrl = apiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/intents/{Uri.EscapeDataString(intentId)}/terminal/hooks/{hookEvent}"
            + $"?mode={Uri.EscapeDataString(mode)}";
        return $"curl -s -X POST {ShellQuote(url)} -H 'Content-Type: application/json' -d @-";
    }

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}
