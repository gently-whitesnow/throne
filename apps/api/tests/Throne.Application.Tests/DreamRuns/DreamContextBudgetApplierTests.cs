using FluentAssertions;
using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class DreamContextBudgetApplierTests
{
    private static readonly DateTimeOffset Base = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Кэп per kind применяется и излишек учитывается в OmittedEvidenceCounts.BudgetExceeded")]
    public void Per_kind_cap_drops_overflow_into_budget_exceeded()
    {
        var budget = new DreamContextBudget(
            MaxReviews: 2,
            MaxQa: 1,
            MaxMcpCalls: 1,
            MaxOutcomes: 1,
            MaxVerifications: 1,
            MaxManualCorrections: 1,
            MaxPatterns: 10);

        var items = new EvidenceItem[]
        {
            new(EvidenceKindNames.Review, "r1", Base, null, false),
            new(EvidenceKindNames.Review, "r2", Base, null, false),
            new(EvidenceKindNames.Review, "r3", Base, null, false),
            new(EvidenceKindNames.Qa, "q1", Base, null, false),
            new(EvidenceKindNames.Qa, "q2", Base, null, false),
            new(EvidenceKindNames.McpCall, "mcp1", Base, null, false),
        };

        var pack = DreamContextBudgetApplier.Apply(items, budget);

        pack.Counts.Reviews.Should().Be(2);
        pack.Counts.Qa.Should().Be(1);
        pack.Counts.McpErrors.Should().Be(1);
        pack.Omitted.BudgetExceeded.Should().Be(2);
        pack.EvidenceRefs.Should().HaveCount(4);
    }

    [Fact(DisplayName = "EvidenceRefs сохраняют порядок из приоритезированного входа")]
    public void Refs_order_is_preserved()
    {
        var pack = DreamContextBudgetApplier.Apply(
            new EvidenceItem[]
            {
                new(EvidenceKindNames.Review, "first", Base, null, false),
                new(EvidenceKindNames.Qa, "second", Base, null, false),
            },
            DreamContextBudget.Default);

        pack.EvidenceRefs.Select(r => r.Id).Should().Equal("first", "second");
    }
}
