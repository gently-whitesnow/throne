using AppSide = Throne.Application.Git.ReviewCommentSide;
using WireSide = Throne.Repositories.Contracts.Generated.ReviewCommentSide;

namespace Throne.Api.Repositories;

/// <summary>
/// Shared application ↔ wire mapping for <c>ReviewCommentSide</c>, reused by both
/// the submit-request path and the comment-feed projection.
/// </summary>
internal static class ReviewCommentSideMapper
{
    public static AppSide ToApp(WireSide side) => side switch
    {
        WireSide.Left => AppSide.Left,
        _ => AppSide.Right,
    };

    public static WireSide? ToWire(AppSide? side) => side switch
    {
        AppSide.Left => WireSide.Left,
        AppSide.Right => WireSide.Right,
        _ => null,
    };
}
