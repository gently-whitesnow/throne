namespace Throne.Application.Git;

/// <summary>
/// Hostname used by the local <c>glab</c> CLI (e.g. <c>gitlab.com</c>,
/// <c>gitlab.example.com</c>). Persisted as a singleton; the first read
/// materialises the default <c>gitlab.com</c>. No env-var fallback.
/// </summary>
public interface IGitLabHostProvider
{
    Task<string> GetHostAsync(CancellationToken ct);

    Task<string> SetHostAsync(string host, CancellationToken ct);
}
