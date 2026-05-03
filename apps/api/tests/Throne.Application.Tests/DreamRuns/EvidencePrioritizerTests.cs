using FluentAssertions;
using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class EvidencePrioritizerTests
{
    private static readonly DateTimeOffset Base = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "High-severity refs идут первыми независимо от kind")]
    public void High_severity_first()
    {
        var items = new EvidenceItem[]
        {
            new(EvidenceKindNames.Qa, "qa-1", Base, SessionId: null, HighSeverity: false),
            new(EvidenceKindNames.Outcome, "out-1", Base, SessionId: null, HighSeverity: true),
        };

        var sorted = EvidencePrioritizer.Prioritize(items);

        sorted[0].Id.Should().Be("out-1");
    }

    [Fact(DisplayName = "review раньше qa, verification раньше mcp_call, mcp_call раньше outcome")]
    public void Kind_priority_order()
    {
        var items = new EvidenceItem[]
        {
            new(EvidenceKindNames.Outcome, "out", Base, null, false),
            new(EvidenceKindNames.McpCall, "mcp", Base, null, false),
            new(EvidenceKindNames.Qa, "qa", Base, null, false),
            new(EvidenceKindNames.Verification, "ver", Base, null, false),
            new(EvidenceKindNames.Review, "rev", Base, null, false),
        };

        var sorted = EvidencePrioritizer.Prioritize(items);

        sorted.Select(i => i.Id).Should().Equal("rev", "qa", "ver", "mcp", "out");
    }

    [Fact(DisplayName = "Свежие записи внутри одного kind идут раньше старых")]
    public void Recent_first_within_kind()
    {
        var items = new EvidenceItem[]
        {
            new(EvidenceKindNames.Review, "old", Base.AddDays(-2), null, false),
            new(EvidenceKindNames.Review, "new", Base.AddDays(-1), null, false),
        };

        var sorted = EvidencePrioritizer.Prioritize(items);

        sorted.Select(i => i.Id).Should().Equal("new", "old");
    }
}
