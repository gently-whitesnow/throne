namespace Throne.Application.Git;

/// <summary>
/// Scope of <see cref="IGitProvider.SearchRepositoriesAsync"/> per ADR-0024 § 3.
/// Mirrors the <c>RepositorySearchScope</c> enum in the OpenAPI contract.
/// </summary>
public enum RepositorySearchScope
{
    /// <summary>Only repositories owned by the authenticated user.</summary>
    Mine = 0,

    /// <summary>
    /// Repositories where the authenticated user is owner, collaborator, or
    /// organisation member. Surfaces a wider set for users who work mostly
    /// in repos they don't own.
    /// </summary>
    Involved = 1,
}
