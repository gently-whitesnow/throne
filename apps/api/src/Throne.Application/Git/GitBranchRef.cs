namespace Throne.Application.Git;

/// <summary>
/// Typed projection of a single branch returned by <see cref="IGitProvider.ListBranchesAsync"/>.
/// Mirrors <c>GitBranchRefDto</c> in <c>specs/contracts/repositories/openapi.yaml</c>
/// but lives in Application so the port stays free of generated DTOs.
/// </summary>
/// <param name="Name">Branch name as reported by upstream (e.g. <c>main</c>).</param>
/// <param name="IsDefault">Whether this is the repository's default branch.</param>
public sealed record GitBranchRef(string Name, bool IsDefault);
