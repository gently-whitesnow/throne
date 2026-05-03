using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Domain.Instructions;

namespace Throne.Application.DreamRuns;

public sealed record RunDreamCommand(string? Policy);

public static class RunDreamPolicies
{
    public const string Auto = "auto";

    public static readonly IReadOnlyList<string> All = [Auto];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public static class RunDreamResultStatuses
{
    public const string Created = "created";
    public const string NotEnoughContext = "not_enough_context";
    public const string ExistingPending = "existing_pending";
}

/// <summary>
/// Result of <see cref="RunDreamHandler"/>. Server-managed: the agent never chooses
/// its own intent window or context size.
/// </summary>
public sealed record RunDreamResult(
    string Status,
    ReadinessSnapshot Readiness,
    DreamRunPayload? DreamRun,
    string? Reason);

public sealed record DreamRunPayload(
    DreamRun Run,
    DreamEvidenceSummary EvidenceSummary,
    IReadOnlyList<IntentRef> IntentRefs);

public sealed record DreamEvidenceSummary(
    int IntentCount,
    int TokenCount,
    IReadOnlyDictionary<string, IReadOnlyList<LearnedRule>> ExistingLearnedRulesByKind);

public sealed class RunDreamHandler(
    IDreamRunRepository runs,
    IInstructionRepository instructions,
    DreamWindowResolver windows,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private static readonly IReadOnlyList<string> AgentInstructionKinds =
    [
        InstructionKindNames.Common,
        InstructionKindNames.Interview,
        InstructionKindNames.Work,
        InstructionKindNames.NewProject,
        InstructionKindNames.Fix,
    ];

    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<RunDreamResult> HandleAsync(RunDreamCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var policy = string.IsNullOrWhiteSpace(command.Policy) ? RunDreamPolicies.Auto : command.Policy.Trim();
        if (!RunDreamPolicies.IsKnown(policy))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown run_dream policy: {policy}.",
                new Dictionary<string, object?> { ["policy"] = policy });
        }

        var assembly = await windows.AssembleAsync(ct);
        var pendingRuns = await runs.ListPendingAsync(ct);
        var pendingProposals = pendingRuns.Sum(r => r.PendingCount);
        var readiness = ReadinessProjector.Project(assembly, pendingProposals, pendingRuns.Count);

        var idempotent = FindIdempotentRun(pendingRuns);
        if (idempotent is not null)
        {
            var summary = await BuildEvidenceSummaryAsync(idempotent, ct);
            return new RunDreamResult(
                RunDreamResultStatuses.ExistingPending,
                readiness,
                new DreamRunPayload(idempotent, summary, idempotent.IntentRefs),
                Reason: null);
        }

        if (assembly.Available.Items.Count == 0)
        {
            return new RunDreamResult(
                RunDreamResultStatuses.NotEnoughContext,
                readiness,
                DreamRun: null,
                Reason: "No intents with qa or review activity in the safe window.");
        }

        var now = clock.GetUtcNow();
        var intentRefs = assembly.AvailableBreakdown
            .Select(b => IntentRef.Create(b.IntentId, b.TokenCount, now))
            .ToList();

        var run = DreamRun.Create(
            DreamRunId.New(),
            assembly.Available.WindowStart,
            assembly.Available.WindowEnd,
            assembly.AvailableTokens,
            intentRefs,
            now);

        await unitOfWork.ExecuteAsync(
            inner => runs.CreateAsync(run, inner),
            ct);

        var evidenceSummary = await BuildEvidenceSummaryAsync(run, ct);
        return new RunDreamResult(
            RunDreamResultStatuses.Created,
            readiness,
            new DreamRunPayload(run, evidenceSummary, run.IntentRefs),
            Reason: null);
    }

    private DreamRun? FindIdempotentRun(IReadOnlyList<DreamRun> pendingRuns)
    {
        if (pendingRuns.Count == 0)
        {
            return null;
        }
        var cutoff = clock.GetUtcNow() - IdempotencyWindow;
        return pendingRuns
            .Where(r => r.CreatedAt >= cutoff)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();
    }

    private async Task<DreamEvidenceSummary> BuildEvidenceSummaryAsync(DreamRun run, CancellationToken ct)
    {
        var existing = await CollectExistingRulesAsync(ct);
        return new DreamEvidenceSummary(run.IntentRefs.Count, run.TokenCount, existing);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<LearnedRule>>> CollectExistingRulesAsync(
        CancellationToken ct)
    {
        var matches = await instructions.GetUserInstructionsByKindsAsync(MvpUser.Id, AgentInstructionKinds, ct);
        var byKind = new Dictionary<string, IReadOnlyList<LearnedRule>>(StringComparer.Ordinal);
        foreach (var kind in AgentInstructionKinds)
        {
            var instruction = matches.FirstOrDefault(m => string.Equals(m.Kind, kind, StringComparison.Ordinal));
            byKind[kind] = instruction is null
                ? Array.Empty<LearnedRule>()
                : LearnedRulesParser.Parse(instruction.Text);
        }
        return byKind;
    }
}
