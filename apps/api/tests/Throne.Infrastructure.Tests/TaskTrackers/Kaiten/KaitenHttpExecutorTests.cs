using System.Net;
using FluentAssertions;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

public class KaitenHttpExecutorTests
{
    [Fact(DisplayName = "GET: bearer-токен и /api/v1 префикс, тело десериализуется")]
    public async Task Get_sends_auth_and_deserializes()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, """{"id":7,"title":"Board","space_id":3}""");

        var board = await executor.GetAsync<KaitenBoard>(KaitenTestHarness.Connection, "/boards/7", CancellationToken.None);

        board.Id.Should().Be(7);
        board.SpaceId.Should().Be(3);
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://acme.kaiten.ru/api/v1/boards/7"));
        request.Authorization.Should().Be("Bearer secret-token");
    }

    [Fact(DisplayName = "POST: тело сериализуется в snake_case, опущенные nullable не уходят")]
    public async Task Post_serializes_snake_case_body()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, """{"id":1,"title":"x","board_id":7,"column_id":3,"condition":1}""");

        await executor.PostAsync<KaitenCard>(
            KaitenTestHarness.Connection,
            "/cards",
            new KaitenCreateCardRequest("New", BoardId: 7, ColumnId: 3),
            CancellationToken.None);

        var body = handler.Requests.Single().Body!;
        body.Should().Contain("\"board_id\":7").And.Contain("\"column_id\":3").And.Contain("\"title\":\"New\"");
        body.Should().NotContain("lane_id").And.NotContain("description");
    }

    [Theory(DisplayName = "Ретраит 429 и 5xx, затем успех")]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Retries_then_succeeds(HttpStatusCode transient)
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(transient).Enqueue(HttpStatusCode.OK, """{"id":5,"title":"t","space_id":1}""");

        var space = await executor.GetAsync<KaitenSpace>(KaitenTestHarness.Connection, "/spaces/5", CancellationToken.None);

        space.Id.Should().Be(5);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Неретраябельный статус (404) бросает сразу, без повтора")]
    public async Task Non_retryable_throws_without_retry()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.NotFound, """{"error":"not found"}""");

        var act = () => executor.GetAsync<KaitenCard>(KaitenTestHarness.Connection, "/cards/1", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<KaitenApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ex.Body.Should().Contain("not found");
        handler.Requests.Should().ContainSingle();
    }

    [Fact(DisplayName = "Исчерпание попыток бросает после MaxAttempts")]
    public async Task Exhausts_attempts_then_throws()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.TooManyRequests)
            .Enqueue(HttpStatusCode.TooManyRequests)
            .Enqueue(HttpStatusCode.TooManyRequests);

        var act = () => executor.GetAsync<KaitenCard>(KaitenTestHarness.Connection, "/cards/1", CancellationToken.None);

        await act.Should().ThrowAsync<KaitenApiException>();
        handler.Requests.Should().HaveCount(3);
    }

    [Fact(DisplayName = "DELETE не требует тела ответа")]
    public async Task Delete_tolerates_empty_body()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK);

        await executor.DeleteAsync(KaitenTestHarness.Connection, "/cards/1/tags/2", CancellationToken.None);

        handler.Requests.Single().Method.Should().Be(HttpMethod.Delete);
    }
}
