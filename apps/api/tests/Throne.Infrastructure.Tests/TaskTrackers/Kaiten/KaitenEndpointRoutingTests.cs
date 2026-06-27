using System.Net;
using FluentAssertions;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

/// <summary>
/// Locks the REST verb+path each endpoint group hits. Guards the non-obvious ones — Kaiten documents
/// card children under the <c>card-children</c> group but serves them on the nested cards path, and
/// add-child puts the child id in the <c>card_id</c> body field with the parent in the path.
/// </summary>
public class KaitenEndpointRoutingTests
{
    private const string Base = "https://acme.kaiten.ru/api/v1";

    [Fact(DisplayName = "Topology: spaces/boards/columns/lanes пути")]
    public async Task Topology_routes()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        for (var i = 0; i < 6; i++)
        {
            handler.Enqueue(HttpStatusCode.OK, "[]");
        }

        var api = new KaitenTopologyApi(executor);
        await api.ListSpacesAsync(KaitenTestHarness.Connection, CancellationToken.None);
        await api.ListBoardsAsync(KaitenTestHarness.Connection, 3, CancellationToken.None);
        await api.ListColumnsAsync(KaitenTestHarness.Connection, 7, CancellationToken.None);
        await api.ListLanesAsync(KaitenTestHarness.Connection, 7, CancellationToken.None);

        handler.Requests[0].Uri.AbsoluteUri.Should().Be($"{Base}/spaces");
        handler.Requests[1].Uri.AbsoluteUri.Should().Be($"{Base}/spaces/3/boards");
        handler.Requests[2].Uri.AbsoluteUri.Should().Be($"{Base}/boards/7/columns");
        handler.Requests[3].Uri.AbsoluteUri.Should().Be($"{Base}/boards/7/lanes");
    }

    [Fact(DisplayName = "Comments и tags: пути и verb")]
    public async Task Comment_and_tag_routes()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, """{"id":1,"text":"hi","type":1,"card_id":42,"author_id":2}""")
            .Enqueue(HttpStatusCode.OK);

        await new KaitenCommentsApi(executor).AddCommentAsync(
            KaitenTestHarness.Connection, 42, new KaitenCreateCommentRequest("hi"), CancellationToken.None);
        await new KaitenTagsApi(executor).RemoveCardTagAsync(
            KaitenTestHarness.Connection, 42, 5, CancellationToken.None);

        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Uri.AbsoluteUri.Should().Be($"{Base}/cards/42/comments");
        handler.Requests[1].Method.Should().Be(HttpMethod.Delete);
        handler.Requests[1].Uri.AbsoluteUri.Should().Be($"{Base}/cards/42/tags/5");
    }

    [Fact(DisplayName = "Card-children: list/add/remove на /cards/{parent}/children, child id в body card_id")]
    public async Task Card_children_routes()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, "[]")
            .Enqueue(HttpStatusCode.OK, """{"id":99,"title":"child","board_id":7,"column_id":3,"condition":1}""")
            .Enqueue(HttpStatusCode.OK);

        var api = new KaitenCardChildrenApi(executor);
        await api.ListChildrenAsync(KaitenTestHarness.Connection, 42, CancellationToken.None);
        await api.AddChildAsync(
            KaitenTestHarness.Connection, 42, KaitenAddCardChildRequest.ForChild(99), CancellationToken.None);
        await api.RemoveChildAsync(KaitenTestHarness.Connection, 42, 99, CancellationToken.None);

        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].Uri.AbsoluteUri.Should().Be($"{Base}/cards/42/children");
        handler.Requests[1].Method.Should().Be(HttpMethod.Post);
        handler.Requests[1].Uri.AbsoluteUri.Should().Be($"{Base}/cards/42/children");
        handler.Requests[1].Body.Should().Contain("\"card_id\":99");
        handler.Requests[2].Method.Should().Be(HttpMethod.Delete);
        handler.Requests[2].Uri.AbsoluteUri.Should().Be($"{Base}/cards/42/children/99");
    }
}
