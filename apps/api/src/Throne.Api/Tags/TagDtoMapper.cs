using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Tags.Contracts.Generated;

namespace Throne.Api.Tags;

internal static class TagDtoMapper
{
    public static TagDto ToDto(Tag tag) => new()
    {
        Id = tag.Id.Value,
        Name = tag.Name,
        Current_version = tag.CurrentVersion,
        Created_at = tag.CreatedAt,
        Updated_at = tag.UpdatedAt,
    };

    public static TagDetailDto ToDetailDto(Tag tag)
    {
        var detail = new TagDetailDto
        {
            Id = tag.Id.Value,
            Name = tag.Name,
            Current_version = tag.CurrentVersion,
            Created_at = tag.CreatedAt,
            Updated_at = tag.UpdatedAt,
            Default_repositories = tag.DefaultRepositories.Select(ToDefaultRepoDto).ToArray(),
        };
        return detail;
    }

    public static TagDefaultRepository FromDefaultRepoDto(TagDefaultRepositoryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var coordinate = new RepoCoordinate(
            Provider: dto.Provider switch
            {
                TagDefaultGitProvider.Github => GitProviderNames.GitHub,
                TagDefaultGitProvider.Gitlab => GitProviderNames.GitLab,
                _ => throw new ArgumentOutOfRangeException(nameof(dto), $"Unknown provider '{dto.Provider}'."),
            },
            Owner: dto.Owner,
            Repo: dto.Repo,
            Host: dto.Host,
            ProjectId: dto.Project_id);
        return new TagDefaultRepository(coordinate, string.IsNullOrWhiteSpace(dto.Default_branch) ? null : dto.Default_branch);
    }

    private static TagDefaultRepositoryDto ToDefaultRepoDto(TagDefaultRepository repo) => new()
    {
        Provider = repo.Coordinate.Provider switch
        {
            GitProviderNames.GitHub => TagDefaultGitProvider.Github,
            GitProviderNames.GitLab => TagDefaultGitProvider.Gitlab,
            _ => throw new InvalidOperationException(
                $"Provider '{repo.Coordinate.Provider}' is not exposed in TagDefaultGitProvider."),
        },
        Host = repo.Coordinate.Host,
        Owner = repo.Coordinate.Owner,
        Repo = repo.Coordinate.Repo,
        Project_id = repo.Coordinate.ProjectId,
        Default_branch = repo.DefaultBranch,
    };
}
