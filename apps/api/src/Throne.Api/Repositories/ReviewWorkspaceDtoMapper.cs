using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;
using AppDiffFileStatus = Throne.Application.Git.PullRequestDiffFileStatus;
using AppSide = Throne.Application.Git.ReviewCommentSide;
using AppSubmitRequest = Throne.Application.Git.SubmitReviewCommentRequest;
using WireDiffFileStatus = Throne.Repositories.Contracts.Generated.PullRequestDiffFileStatus;
using WireSide = Throne.Repositories.Contracts.Generated.ReviewCommentSide;
using WireSubmitRequest = Throne.Repositories.Contracts.Generated.SubmitReviewCommentRequest;

namespace Throne.Api.Repositories;

/// <summary>
/// Wire-format ↔ application translation for the Slice 4A review workspace surface
/// (diff / commits / submit comment).
/// </summary>
internal static class ReviewWorkspaceDtoMapper
{
    public static PullRequestDiffDto ToDiffDto(PullRequestDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var dto = new PullRequestDiffDto
        {
            Base_sha = diff.BaseSha,
            Head_sha = diff.HeadSha,
            Start_sha = diff.StartSha,
        };
        foreach (var file in diff.Files)
        {
            dto.Files.Add(new PullRequestDiffFileDto
            {
                Path = file.Path,
                Previous_path = file.PreviousPath,
                Status = ToWireDiffFileStatus(file.Status),
                Patch = file.Patch,
            });
        }
        return dto;
    }

    public static PullRequestCommitDto ToCommitDto(PullRequestCommitRef commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return new PullRequestCommitDto
        {
            Sha = commit.Sha,
            Message = commit.Message,
            Author_login = commit.AuthorLogin,
            Committed_at = commit.CommittedAt,
        };
    }

    public static SubmittedReviewCommentDto ToSubmittedDto(SubmittedReviewComment comment, string bindingId)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return new SubmittedReviewCommentDto
        {
            Id = comment.Id,
            Binding_id = bindingId,
            Author_login = comment.AuthorLogin,
            Body = comment.Body,
            Html_url = PullRequestCommentDtoMapper.ToUri(comment.HtmlUrl),
            Created_at = comment.CreatedAt,
        };
    }

    public static AppSubmitRequest ToSubmitRequest(WireSubmitRequest dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AppSubmitRequest(
            Body: dto.Content,
            Path: dto.Path,
            PreviousPath: dto.Previous_path,
            Side: ToAppSide(dto.Side),
            Line: dto.Line,
            CommitSha: dto.Commit_sha,
            BaseSha: dto.Base_sha,
            StartSha: dto.Start_sha);
    }

    private static WireDiffFileStatus ToWireDiffFileStatus(AppDiffFileStatus status) => status switch
    {
        AppDiffFileStatus.Added => WireDiffFileStatus.Added,
        AppDiffFileStatus.Removed => WireDiffFileStatus.Removed,
        AppDiffFileStatus.Renamed => WireDiffFileStatus.Renamed,
        AppDiffFileStatus.Copied => WireDiffFileStatus.Copied,
        _ => WireDiffFileStatus.Modified,
    };

    private static AppSide ToAppSide(WireSide side) => side switch
    {
        WireSide.Left => AppSide.Left,
        _ => AppSide.Right,
    };
}
