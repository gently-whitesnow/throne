using System.Text.RegularExpressions;

namespace Throne.Domain.Repositories;

/// <summary>
/// Review D5 — normalises the ETag echoed by the upstream PR review-comments
/// endpoint before it is persisted on <see cref="IntentRepositoryBindingState.ReviewCommentsEtag"/>.
/// Anything outside RFC 7230 visible-ASCII (CR/LF, control chars, header-injection
/// attempts, mojibake from a misconfigured proxy) is dropped to <see langword="null"/>
/// so the next polling cycle issues a full fetch instead of attaching an unsafe
/// <c>If-None-Match</c> header.
///
/// Separated from <see cref="IntentRepositoryBindingPullRequestMutator"/> so that
/// per-type still holds inside its CA1502 cyclomatic budget.
/// </summary>
public static partial class ReviewCommentsEtagNormalizer
{
    [GeneratedRegex(@"^[\x21-\x7e]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static string? Normalize(string? etag) =>
        !string.IsNullOrWhiteSpace(etag) && Pattern().IsMatch(etag) ? etag : null;
}
