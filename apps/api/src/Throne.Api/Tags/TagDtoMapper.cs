using Throne.Application.Tags;
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

    public static TagListItemDto ToListItemDto(TagListItem item) => new()
    {
        Id = item.Tag.Id.Value,
        Name = item.Tag.Name,
        Current_version = item.Tag.CurrentVersion,
        Created_at = item.Tag.CreatedAt,
        Updated_at = item.Tag.UpdatedAt,
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
            Provider: dto.Provider,
            Owner: dto.Owner,
            Repo: dto.Repo,
            Host: dto.Host,
            ProjectId: dto.Project_id);
        return new TagDefaultRepository(coordinate, string.IsNullOrWhiteSpace(dto.Default_branch) ? null : dto.Default_branch);
    }

    private static TagDefaultRepositoryDto ToDefaultRepoDto(TagDefaultRepository repo) => new()
    {
        Provider = repo.Coordinate.Provider,
        Host = repo.Coordinate.Host,
        Owner = repo.Coordinate.Owner,
        Repo = repo.Coordinate.Repo,
        Project_id = repo.Coordinate.ProjectId,
        Default_branch = repo.DefaultBranch,
    };
}
