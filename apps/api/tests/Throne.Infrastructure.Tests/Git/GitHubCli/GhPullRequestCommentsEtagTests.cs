using FluentAssertions;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Round-trip and legacy-decoding coverage for the composite ETag persisted on
/// the binding. Encoded output stays inside the visible-ASCII subset accepted
/// by <see cref="ReviewCommentsEtagNormalizer"/> so the domain normalizer does
/// not strip it away.
/// </summary>
public class GhPullRequestCommentsEtagTests
{
    [Fact(DisplayName = "Decode возвращает оба null при пустом etag")]
    public void Decode_returns_pair_of_nulls_when_empty()
    {
        var decoded = GhPullRequestCommentsEtag.Decode(null);

        decoded.Issues.Should().BeNull();
        decoded.Review.Should().BeNull();
    }

    [Fact(DisplayName = "Decode распознаёт легаси-строку как review-only etag")]
    public void Decode_treats_non_json_as_legacy_review_etag()
    {
        var decoded = GhPullRequestCommentsEtag.Decode("W/\"abc\"");

        decoded.Issues.Should().BeNull();
        decoded.Review.Should().Be("W/\"abc\"");
    }

    [Fact(DisplayName = "Encode/Decode round-trip сохраняет оба значения")]
    public void Encode_decode_round_trip_preserves_both_sides()
    {
        var encoded = GhPullRequestCommentsEtag.Encode("\"ie\"", "W/\"re\"");

        var decoded = GhPullRequestCommentsEtag.Decode(encoded);
        decoded.Issues.Should().Be("\"ie\"");
        decoded.Review.Should().Be("W/\"re\"");
    }

    [Fact(DisplayName = "Encode даёт null когда обе стороны null")]
    public void Encode_returns_null_when_both_nulls()
    {
        var encoded = GhPullRequestCommentsEtag.Encode(null, null);

        encoded.Should().BeNull();
    }

    [Fact(DisplayName = "Encode проходит через ReviewCommentsEtagNormalizer без потерь")]
    public void Encoded_value_survives_normalizer()
    {
        var encoded = GhPullRequestCommentsEtag.Encode("\"ie\"", "W/\"re\"");

        var normalized = ReviewCommentsEtagNormalizer.Normalize(encoded);
        normalized.Should().Be(encoded);
    }
}
