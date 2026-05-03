// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class DreamTools(
    GetDreamReadinessHandler getReadiness,
    RunDreamHandler runDream,
    ProposeDreamRuleHandler proposeRule,
    CloseEmptyDreamRunHandler closeEmpty)
{
    [McpServerTool(Name = "get_dream_readiness", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read-only readiness snapshot of the self-learning fuel meter. Used by /tdream before deciding whether to call run_dream and by UI for the fuel widget. Lightweight — does not lock or mutate evidence.")]
    public async Task<DreamReadinessDto> GetDreamReadiness(CancellationToken cancellationToken = default)
    {
        var snapshot = await getReadiness.HandleAsync(new GetDreamReadinessQuery(), cancellationToken);
        return DreamMcpDtoMapper.ToReadiness(snapshot);
    }

    [McpServerTool(Name = "run_dream", UseStructuredContent = true)]
    [Description("Create a new pending DreamRun if there is enough fresh evidence. Server-managed: the agent does not pick the window or evidence size — it only inspects readiness, applies idempotency over the last 24h, prioritizes raw signals, and returns an evidence_summary the agent can reason over before calling propose_dream_rule. Returns status='not_enough_context' or 'existing_pending' instead of creating empty/duplicate runs.")]
    public async Task<RunDreamResultDto> RunDream(
        [Description("Reserved for future shaping (\"minimal\", \"rich\"). MVP accepts only 'auto' (default).")] string? policy = null,
        CancellationToken cancellationToken = default)
    {
        var result = await runDream.HandleAsync(new RunDreamCommand(policy), cancellationToken);
        return DreamMcpDtoMapper.ToRunDreamResult(result);
    }

    [McpServerTool(Name = "propose_dream_rule", UseStructuredContent = true)]
    [Description("Propose a single learned rule against a still-pending DreamRun. evidence_refs must be a subset of the run's snapshot — agents cannot reference raw evidence the server did not include. target_kind is restricted to user instructions: common, interview, work, new_project, fix. /throne and /dream kinds are out of scope here.")]
    public async Task<ProposeDreamRuleResultDto> ProposeDreamRule(
        [Description("DreamRun id this proposal belongs to (must still be pending).")] string run_id,
        [Description("Target user instruction kind: 'common', 'interview', 'work', 'new_project', or 'fix'. Other kinds are rejected.")] string target_kind,
        [Description("The proposed bullet to append under the target's `## Learned rules` section. At most 280 characters.")] string proposed_rule,
        [Description("Subset of run.evidence_refs justifying this proposal. Severity dictates the minimum: high>=1, medium>=2, low>=3.")] IReadOnlyList<EvidenceRefInput> evidence_refs,
        [Description("Why the rule matters. Short, diagnostic prose that an end user can audit later.")] string rationale,
        [Description("Severity of the underlying signal: 'high', 'medium', or 'low'.")] string severity,
        CancellationToken cancellationToken = default)
    {
        var refs = MapEvidenceRefs(evidence_refs);
        var result = await proposeRule.HandleAsync(
            new ProposeDreamRuleCommand(run_id, target_kind, proposed_rule, refs, rationale, severity),
            cancellationToken);
        return DreamMcpDtoMapper.ToProposeResult(result);
    }

    [McpServerTool(Name = "close_empty_dream_run", UseStructuredContent = true)]
    [Description("Close a still-pending DreamRun that produced no proposals. The agent uses this to release the locked evidence back into the next /tdream window. Forced closes of runs WITH proposals are rejected (409) — that path is reserved for the user via UI/HTTP, so the agent cannot mask its own dropped suggestions.")]
    public async Task<DreamRunDto> CloseEmptyDreamRun(
        [Description("DreamRun id to close. Must be pending and have zero proposals.")] string run_id,
        [Description("If true (default), the run's evidence is released to be reconsidered next time. If false, the evidence is marked processed and will not resurface.")] bool? release_evidence = null,
        CancellationToken cancellationToken = default)
    {
        var run = await closeEmpty.HandleAsync(
            new CloseEmptyDreamRunCommand(run_id, release_evidence),
            cancellationToken);
        return DreamMcpDtoMapper.ToRun(run);
    }

    private static IReadOnlyList<EvidenceRef> MapEvidenceRefs(IReadOnlyList<EvidenceRefInput>? input)
    {
        if (input is null || input.Count == 0)
        {
            return Array.Empty<EvidenceRef>();
        }
        var result = new List<EvidenceRef>(input.Count);
        foreach (var entry in input)
        {
            if (entry is null)
            {
                continue;
            }
            result.Add(new EvidenceRef(entry.Kind ?? string.Empty, entry.Id ?? string.Empty, entry.CreatedAt));
        }
        return result;
    }
}

public sealed record EvidenceRefInput(
    [property: Description("Evidence kind: 'review', 'qa', 'mcp_call', 'outcome', 'verification', or 'manual_correction'.")] string Kind,
    [property: Description("Evidence record id within the source collection.")] string Id,
    [property: Description("Optional creation timestamp (UTC). Echoed back from run.evidence_refs to keep the request self-contained.")] DateTimeOffset? CreatedAt);
