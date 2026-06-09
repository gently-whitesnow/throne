using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Projects an upstream-fresh <see cref="PullRequestComment"/> onto the wire
/// <see cref="PullRequestCommentDto"/>. The server does not persist comment
/// bodies — the <see cref="BindingId"/> comes from the binding context (HTTP
/// path, MCP iteration, realtime fanout).
/// </summary>
internal static class PullRequestCommentDtoMapper
{
    public static PullRequestCommentDto ToDto(PullRequestComment comment, BindingId bindingId)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return new PullRequestCommentDto
        {
            Id = comment.Id,
            Binding_id = bindingId.Value,
            Author_login = comment.AuthorLogin,
            Author_avatar_url = ToUri(comment.AuthorAvatarUrl),
            Body = comment.Body,
            Html_url = ToUri(comment.HtmlUrl),
            Path = comment.Path,
            Created_at = comment.CreatedAt,
            Updated_at = comment.UpdatedAt,
            Line = comment.Line,
            Side = ReviewCommentSideMapper.ToWire(comment.Side),
            Resolved = comment.Resolved,
            Thread_id = comment.ThreadId,
        };
    }

    internal static Uri? ToUri(string? value) => value is null ? null : new Uri(value);
}
