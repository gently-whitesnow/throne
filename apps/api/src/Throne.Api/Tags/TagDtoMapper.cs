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
}
