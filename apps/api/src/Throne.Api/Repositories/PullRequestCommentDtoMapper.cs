using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Projects a stored <see cref="PullRequestCommentRecord"/> onto the wire
/// <see cref="PullRequestCommentDto"/>. Split out of <see cref="RepositoryDtoMapper"/>
/// so the per-class CA1502 cyclomatic budget holds.
/// </summary>
internal static class PullRequestCommentDtoMapper
{
    public static PullRequestCommentDto ToDto(PullRequestCommentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new PullRequestCommentDto
        {
            Id = record.UpstreamId,
            Binding_id = record.BindingId.Value,
            Author_login = record.AuthorLogin,
            Author_avatar_url = ToUri(record.AuthorAvatarUrl),
            Body = record.Body,
            Html_url = ToUri(record.HtmlUrl),
            Path = record.Path,
            Created_at = record.CreatedAt,
            Updated_at = record.UpdatedAt,
        };
    }

    internal static Uri? ToUri(string? value) => value is null ? null : new Uri(value);
}
