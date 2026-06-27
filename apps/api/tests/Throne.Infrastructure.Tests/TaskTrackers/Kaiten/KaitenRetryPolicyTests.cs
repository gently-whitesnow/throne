using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Http;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

public class KaitenRetryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "Retryable: 429 и любой 5xx")]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public void Classifies_retryable_statuses(HttpStatusCode status, bool expected) =>
        KaitenRetryPolicy.IsRetryable(status).Should().Be(expected);

    [Fact(DisplayName = "Retry-After в секундах имеет приоритет над backoff")]
    public void Honours_retry_after_delta()
    {
        var policy = new KaitenRetryPolicy(new KaitenOptions { RetryBaseDelayMilliseconds = 1, RetryMaxDelayMilliseconds = 1 });
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        policy.NextDelay(1, response, Now).Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact(DisplayName = "Retry-After как HTTP-дата переводится в относительную задержку")]
    public void Honours_retry_after_date()
    {
        var policy = new KaitenRetryPolicy(new KaitenOptions());
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(12));

        policy.NextDelay(1, response, Now).Should().Be(TimeSpan.FromSeconds(12));
    }

    [Fact(DisplayName = "Без Retry-After — экспоненциальный backoff с потолком")]
    public void Falls_back_to_capped_exponential_backoff()
    {
        var policy = new KaitenRetryPolicy(new KaitenOptions { RetryBaseDelayMilliseconds = 100, RetryMaxDelayMilliseconds = 500 });
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        policy.NextDelay(1, response, Now).Should().Be(TimeSpan.FromMilliseconds(100));
        policy.NextDelay(2, response, Now).Should().Be(TimeSpan.FromMilliseconds(200));
        policy.NextDelay(3, response, Now).Should().Be(TimeSpan.FromMilliseconds(400));
        policy.NextDelay(4, response, Now).Should().Be(TimeSpan.FromMilliseconds(500));
    }
}
