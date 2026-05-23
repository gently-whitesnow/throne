using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// One-shot interpretation of a <c>gh api -i</c> invocation for the PR surface:
/// splits the response and lifts the status code so callers can read 304 / 404
/// directly. Rate-limit detection and the success-or-throw decision live in
/// <see cref="GhPrResponseGuard"/> so this record stays inside the CA1502
/// per-type cyclomatic budget.
/// </summary>
internal readonly record struct GhPrResponse(
    int Status,
    string Body,
    Dictionary<string, string> Headers)
{
    public static GhPrResponse From(ProcessRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var raw = result.StandardOutput ?? string.Empty;
        var split = GhHttpResponseSplitter.Split(raw);
        return new GhPrResponse(GhHttpStatus.ParseStatusCode(raw), split.Body, split.Headers);
    }

    public string? Etag => GhHttpStatus.ReadEtag(Headers);

    public bool IsNotFound => Status == 404;

    public bool IsNotModified => Status == 304;
}
