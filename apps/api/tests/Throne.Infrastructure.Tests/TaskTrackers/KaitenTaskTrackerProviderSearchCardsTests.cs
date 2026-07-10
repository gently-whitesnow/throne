using FluentAssertions;
using Throne.Infrastructure.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;
using static Throne.Infrastructure.Tests.TaskTrackers.KaitenProviderTestHarness;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public sealed class KaitenTaskTrackerProviderSearchCardsTests
{
    [Fact(DisplayName = "SearchCards → empty query asks Kaiten for updated_at desc + limit")]
    public async Task Empty_query_uses_updated_sort()
    {
        KaitenCardQuery? seen = null;
        var provider = Provider(
            listCards: (_, query, _) =>
            {
                seen = query;
                return Task.FromResult<IReadOnlyList<KaitenCard>>(
                [
                    SampleCard(columnId: 100, id: 1) with { Updated = DateTimeOffset.UnixEpoch },
                    SampleCard(columnId: 100, id: 2) with { Updated = DateTimeOffset.UnixEpoch.AddMinutes(5) },
                ]);
            },
            columns: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>(
                [new KaitenColumn(100, "In Progress", 10, 0)]));

        var cards = await provider.SearchCardsAsync(Descriptor, "10", query: null, limit: 10, CancellationToken.None);

        seen.Should().NotBeNull();
        seen!.Query.Should().BeNull();
        seen.OrderBy.Should().Be("updated");
        seen.OrderDirection.Should().Be("desc");
        seen.Limit.Should().Be(10);
        seen.Condition.Should().Be(KaitenCardConditions.Live);
        // Belt-and-suspenders: sorted by UpdatedAt desc even if Kaiten ignored order_by.
        cards.Select(c => c.CardId).Should().ContainInOrder("2", "1");
    }

    [Fact(DisplayName = "SearchCards → non-empty query forwards text filter, drops sort")]
    public async Task Query_forwards_text_filter()
    {
        KaitenCardQuery? seen = null;
        var provider = Provider(
            listCards: (_, query, _) =>
            {
                seen = query;
                return Task.FromResult<IReadOnlyList<KaitenCard>>([SampleCard(columnId: 100)]);
            },
            columns: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>(
                [new KaitenColumn(100, "In Progress", 10, 0)]));

        _ = await provider.SearchCardsAsync(Descriptor, "10", "  bug  ", limit: 5, CancellationToken.None);

        seen.Should().NotBeNull();
        seen!.Query.Should().Be("bug");
        seen.OrderBy.Should().BeNull();
        seen.OrderDirection.Should().BeNull();
        seen.Limit.Should().Be(5);
    }

    [Fact(DisplayName = "SearchCards → archived cards excluded from result")]
    public async Task Excludes_archived()
    {
        var provider = Provider(
            listCards: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenCard>>(
            [
                SampleCard(columnId: 100, id: 1),
                SampleCard(columnId: 100, id: 2, condition: KaitenCardConditions.Archived),
            ]),
            columns: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>(
                [new KaitenColumn(100, "In Progress", 10, 0)]));

        var cards = await provider.SearchCardsAsync(Descriptor, "10", "x", limit: 10, CancellationToken.None);

        cards.Should().ContainSingle().Which.CardId.Should().Be("1");
    }
}
