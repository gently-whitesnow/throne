// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.DreamRuns;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class DreamTools(
    RunDreamHandler runDream,
    ProposeDreamRuleHandler proposeRule)
{
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
    [Description("Propose a single learned rule against a still-pending DreamRun. intent_refs must be a subset of the run's snapshot — agents cannot reference intents the server did not include. target_kind is restricted to user instructions: common, interview, work, fix. The dream kind is out of scope here.")]
    public async Task<ProposeDreamRuleResultDto> ProposeDreamRule(
        [Description("DreamRun id this proposal belongs to (must still be pending).")] string run_id,
        [Description("Target user instruction kind: 'common', 'interview', 'work', or 'fix'. Other kinds are rejected.")] string target_kind,
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
}
