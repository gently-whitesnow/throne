// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.DreamRuns;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class DreamTools(
    GetDreamReadinessHandler getReadiness,
    RunDreamHandler runDream,
    ProposeDreamRuleHandler proposeRule,
    CloseEmptyDreamRunHandler closeEmpty)
{
    [McpServerTool(Name = "get_dream_readiness", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read-only readiness snapshot of the /dream context fuel meter. Returns the count of unique-content tokens currently in the safe window plus the number of distinct intents. Status is informational and never blocks /dream.")]
    public async Task<DreamReadinessDto> GetDreamReadiness(CancellationToken cancellationToken = default)
    {
        var snapshot = await getReadiness.HandleAsync(new GetDreamReadinessQuery(), cancellationToken);
        return DreamMcpDtoMapper.ToReadiness(snapshot);
    }

    [McpServerTool(Name = "run_dream", UseStructuredContent = true)]
    [Description("Create a new pending DreamRun if any intent has fresh qa/review activity in the safe window. Server-managed: the agent does not pick the window or context size. Idempotent over the last 24h. Returns status='not_enough_context' when no intents qualify or 'existing_pending' if an open run was created recently.")]
    public async Task<RunDreamResultDto> RunDream(
        [Description("Reserved for future shaping (\"minimal\", \"rich\"). MVP accepts only 'auto' (default).")] string? policy = null,
        CancellationToken cancellationToken = default)
    {
        var result = await runDream.HandleAsync(new RunDreamCommand(policy), cancellationToken);
        return DreamMcpDtoMapper.ToRunDreamResult(result);
    }

    [McpServerTool(Name = "propose_dream_rule", UseStructuredContent = true)]
    [Description("Propose a single learned rule against a still-pending DreamRun. intent_refs must be a subset of the run's snapshot — agents cannot reference intents the server did not include. target_kind is restricted to user instructions: common, interview, work, new_project, fix. /throne and /dream kinds are out of scope here.")]
    public async Task<ProposeDreamRuleResultDto> ProposeDreamRule(
        [Description("DreamRun id this proposal belongs to (must still be pending).")] string run_id,
        [Description("Target user instruction kind: 'common', 'interview', 'work', 'new_project', or 'fix'. Other kinds are rejected.")] string target_kind,
        [Description("The proposed bullet to append under the target's `## Learned rules` section. At most 280 characters.")] string proposed_rule,
        [Description("Subset of run.intent_refs justifying this proposal. Severity dictates the minimum distinct intents: high>=1, medium>=2, low>=3.")] IReadOnlyList<string> intent_refs,
        [Description("Why the rule matters. Short, diagnostic prose that an end user can audit later.")] string rationale,
        [Description("Severity of the underlying signal: 'high', 'medium', or 'low'.")] string severity,
        CancellationToken cancellationToken = default)
    {
        var result = await proposeRule.HandleAsync(
            new ProposeDreamRuleCommand(
                run_id,
                target_kind,
                proposed_rule,
                intent_refs ?? Array.Empty<string>(),
                rationale,
                severity),
            cancellationToken);
        return DreamMcpDtoMapper.ToProposeResult(result);
    }

    [McpServerTool(Name = "close_empty_dream_run", UseStructuredContent = true)]
    [Description("Close a still-pending DreamRun that produced no proposals. The agent uses this to release the locked intents back into the next /dream window. Forced closes of runs WITH proposals are rejected (409) — that path is reserved for the user via UI/HTTP.")]
    public async Task<DreamRunDto> CloseEmptyDreamRun(
        [Description("DreamRun id to close. Must be pending and have zero proposals.")] string run_id,
        [Description("If true (default), the run's intents are released to be reconsidered next time. If false, the intents are marked processed and will not resurface.")] bool? release_evidence = null,
        CancellationToken cancellationToken = default)
    {
        var run = await closeEmpty.HandleAsync(
            new CloseEmptyDreamRunCommand(run_id, release_evidence),
            cancellationToken);
        return DreamMcpDtoMapper.ToRun(run);
    }
}
