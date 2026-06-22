using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// Provider-neutral core for <see cref="IAgentVendorLoginProbe"/>: runs a vendor's
/// non-interactive «am I logged in» status command via <see cref="IProcessLauncher"/> and
/// folds the outcome into <see cref="AgentVendorLoginStatus"/>. Per-vendor adapters supply
/// only the command, its arguments, and a login hint — no vendor-specific branching leaks
/// into the mapper or the catalog.
///
/// Mapping: process-launch <c>Win32Exception</c> → <see cref="AgentVendorLoginStatus.Missing"/>
/// (CLI absent); exit 0 → <see cref="AgentVendorLoginStatus.Ready"/>; any other exit (or a
/// timeout) → <see cref="AgentVendorLoginStatus.LoggedOut"/>. Never throws.
/// </summary>
internal abstract class CliLoginProbe(IProcessLauncher launcher) : IAgentVendorLoginProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public abstract string Vendor { get; }

    /// <summary>Executable name resolved on PATH (e.g. <c>claude</c>).</summary>
    protected abstract string FileName { get; }

    /// <summary>Status sub-command that exits 0 iff authenticated.</summary>
    protected abstract IReadOnlyList<string> StatusArguments { get; }

    /// <summary>Command the operator runs to authenticate, shown when logged out.</summary>
    protected abstract string LoginHint { get; }

    public async Task<AgentVendorLoginResult> ProbeAsync(CancellationToken ct)
    {
        var request = new ProcessRunRequest(FileName, StatusArguments, Timeout: ProbeTimeout);

        ProcessRunResult result;
        try
        {
            result = await launcher.RunAsync(request, ct);
        }
        catch (TimeoutException)
        {
            return new AgentVendorLoginResult(AgentVendorLoginStatus.LoggedOut, $"{FileName}: status timed out");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new AgentVendorLoginResult(AgentVendorLoginStatus.Missing, $"{FileName} не найден в PATH");
        }

        if (result.ExitCode == 0)
        {
            return new AgentVendorLoginResult(AgentVendorLoginStatus.Ready, ReadableDetail(result.StandardOutput));
        }

        return new AgentVendorLoginResult(AgentVendorLoginStatus.LoggedOut, LoginHint);
    }

    /// <summary>
    /// First human-readable stdout line as the «logged in as …» subtitle. JSON-looking
    /// output (some CLIs print a body by default) is dropped so the card never shows a raw
    /// brace; the status badge alone then conveys readiness.
    /// </summary>
    private static string? ReadableDetail(string stdout)
    {
        var line = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(line) || line.StartsWith('{') || line.StartsWith('['))
        {
            return null;
        }
        return line.Length > 80 ? line[..80] : line;
    }
}

/// <summary>claude login probe — <c>claude auth status --text</c> (exit 0 iff signed in).</summary>
internal sealed class ClaudeLoginProbe(IProcessLauncher launcher) : CliLoginProbe(launcher)
{
    public override string Vendor => TerminalAgentCatalog.VendorClaude;
    protected override string FileName => "claude";
    protected override IReadOnlyList<string> StatusArguments => ["auth", "status", "--text"];
    protected override string LoginHint => "claude auth login";
}

/// <summary>codex login probe — <c>codex login status</c> (exit 0 iff signed in).</summary>
internal sealed class CodexLoginProbe(IProcessLauncher launcher) : CliLoginProbe(launcher)
{
    public override string Vendor => TerminalAgentCatalog.VendorCodex;
    protected override string FileName => "codex";
    protected override IReadOnlyList<string> StatusArguments => ["login", "status"];
    protected override string LoginHint => "codex login";
}
