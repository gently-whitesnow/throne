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
/// Result of <see cref="RunDreamHandler"/>. Server-managed: the agent never
/// chooses its own evidence window or context size. <see cref="Status"/> selects
/// which optional payload is meaningful (Intent 4 §run_dream).
/// </summary>
public sealed record RunDreamResult(
    string Status,
    ReadinessSnapshot Readiness,
    DreamRunPayload? DreamRun,
    string? Reason);

public sealed record DreamRunPayload(
    DreamRun Run,
    DreamEvidenceSummary EvidenceSummary,
    IReadOnlyList<EvidenceRef> EvidenceRefs);

public sealed record DreamEvidenceSummary(
    EvidenceCounts Counts,
    IReadOnlyList<DreamEvidencePattern> Patterns,
    IReadOnlyList<string> SuggestedTargetKinds,
    IReadOnlyDictionary<string, IReadOnlyList<LearnedRule>> ExistingLearnedRulesByKind);

public sealed record DreamEvidencePattern(string Kind, int Count, bool HighSeverity);

public sealed class RunDreamHandler(
    IDreamRunRepository runs,
    IInstructionRepository instructions,
    DreamWindowResolver windows,
    DreamOptions options,
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

    private static readonly DreamContextBudget Budget = DreamContextBudget.Default;

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
        var calculator = new ReadinessCalculator(options);
        var readiness = calculator.Calculate(
            assembly.Window,
            pendingProposals,
            pendingRuns.Count,
            assembly.LockedScore);

        var idempotent = FindIdempotentRun(pendingRuns);
        if (idempotent is not null)
        {
            var summary = await BuildEvidenceSummaryAsync(idempotent, ct);
            return new RunDreamResult(
                RunDreamResultStatuses.ExistingPending,
                readiness,
                new DreamRunPayload(idempotent, summary, idempotent.EvidenceRefs),
                Reason: null);
        }

        if (readiness.Status is ReadinessStatusNames.Empty or ReadinessStatusNames.WarmingUp)
        {
            return new RunDreamResult(
                RunDreamResultStatuses.NotEnoughContext,
                readiness,
                DreamRun: null,
                Reason: $"Only {readiness.EvidenceCounts.Total} items in safe window, threshold={readiness.Threshold}");
        }

        var prioritized = EvidencePrioritizer.Prioritize(assembly.Window.Items);
        var pack = DreamContextBudgetApplier.Apply(prioritized, Budget);
        var score = calculator.ScoreFor(assembly.Window.Items);
        var now = clock.GetUtcNow();
        var run = DreamRun.Create(
            DreamRunId.New(),
            assembly.Window.WindowStart,
            assembly.Window.WindowEnd,
            score,
            pack.Counts,
            pack.EvidenceRefs,
            pack.Omitted,
            now);

        await unitOfWork.ExecuteAsync(
            inner => runs.CreateAsync(run, inner),
            ct);

        var evidenceSummary = await BuildEvidenceSummaryAsync(run, ct);
        return new RunDreamResult(
            RunDreamResultStatuses.Created,
            readiness,
            new DreamRunPayload(run, evidenceSummary, run.EvidenceRefs),
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
        var patterns = BuildPatterns(run.EvidenceRefs, run.EvidenceCounts);
        var suggestedKinds = SuggestTargetKinds(run.EvidenceCounts, patterns);
        var existing = await CollectExistingRulesAsync(ct);
        return new DreamEvidenceSummary(run.EvidenceCounts, patterns, suggestedKinds, existing);
    }

    private static DreamEvidencePattern[] BuildPatterns(
        IReadOnlyList<EvidenceRef> refs,
        EvidenceCounts counts)
    {
        var patterns = new List<DreamEvidencePattern>();
        AddIfPresent(patterns, EvidenceKindNames.ManualCorrection, counts.ManualCorrections);
        AddIfPresent(patterns, EvidenceKindNames.Review, counts.Reviews);
        AddIfPresent(patterns, EvidenceKindNames.Verification, counts.VerificationFailures);
        AddIfPresent(patterns, EvidenceKindNames.McpCall, counts.McpErrors);
        AddIfPresent(patterns, EvidenceKindNames.Qa, counts.Qa);
        AddIfPresent(patterns, EvidenceKindNames.Outcome, counts.AcceptedOutcomes);

        return patterns
            .Take(Budget.MaxPatterns)
            .ToArray();
    }

    private static void AddIfPresent(List<DreamEvidencePattern> sink, string kind, int count)
    {
        if (count > 0)
        {
            sink.Add(new DreamEvidencePattern(kind, count, HighSeverity: false));
        }
    }

    private static string[] SuggestTargetKinds(
        EvidenceCounts counts,
        DreamEvidencePattern[] patterns)
    {
        var suggestions = new List<string>();
        if (counts.Reviews > 0 || counts.VerificationFailures > 0)
        {
            suggestions.Add(InstructionKindNames.Work);
        }
        if (counts.Qa > 0)
        {
            suggestions.Add(InstructionKindNames.Interview);
        }
        if (counts.ManualCorrections > 0)
        {
            suggestions.Add(InstructionKindNames.Common);
        }
        if (suggestions.Count == 0 && patterns.Length > 0)
        {
            suggestions.Add(InstructionKindNames.Common);
        }
        return suggestions.Distinct(StringComparer.Ordinal).ToArray();
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
