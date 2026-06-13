using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;
using AppDiffFileStatus = Throne.Application.Git.PullRequestDiffFileStatus;
using AppMergeRequest = Throne.Application.Git.MergePullRequestRequest;
using AppSubmitRequest = Throne.Application.Git.SubmitReviewCommentRequest;
using WireDiffFileStatus = Throne.Repositories.Contracts.Generated.PullRequestDiffFileStatus;
using WireMergeRequest = Throne.Repositories.Contracts.Generated.MergePullRequestRequest;
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

    public static ReviewFileLinesDto ToFileLinesDto(RepositoryFileLineSlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        var dto = new ReviewFileLinesDto
        {
            From = slice.From,
            To = slice.To,
            Total_lines = slice.TotalLines,
        };
        foreach (var line in slice.Lines)
        {
            dto.Lines.Add(new ReviewFileLineDto
            {
                Line = line.Line,
                Content = line.Content,
            });
        }
        return dto;
    }

    public static PullRequestHeaderDto ToHeaderDto(PullRequestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new PullRequestHeaderDto
        {
            Number = snapshot.Number,
            Title = snapshot.Title,
            State = RepositoryEnumDtoMapper.ToWirePullRequestState(snapshot.State),
            Author_login = snapshot.AuthorLogin,
            Author_avatar_url = PullRequestCommentDtoMapper.ToUri(snapshot.AuthorAvatarUrl),
            Head_ref = snapshot.HeadRef,
            Base_ref = snapshot.BaseRef,
            Html_url = PullRequestCommentDtoMapper.ToUri(snapshot.HtmlUrl),
            Body = snapshot.Body,
        };
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
            Side: ReviewCommentSideMapper.ToApp(dto.Side),
            OldLine: dto.Old_line,
            NewLine: dto.New_line,
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

    public static ReviewThreadDto ToThreadDto(ReviewThreadState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new ReviewThreadDto
        {
            Thread_id = state.ThreadId,
            Resolved = state.Resolved,
        };
    }

    public static PullRequestMergeStatusDto ToMergeStatusDto(PullRequestMergeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new PullRequestMergeStatusDto
        {
            Mergeability = RepositoryEnumDtoMapper.ToWireMergeability(status.Mergeability),
            Checks = RepositoryEnumDtoMapper.ToWireChecksState(status.Checks),
            Html_url = PullRequestCommentDtoMapper.ToUri(status.HtmlUrl),
        };
    }

    public static MergePullRequestResultDto ToMergeResultDto(PullRequestMergeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MergePullRequestResultDto
        {
            Merged = result.Merged,
            Pull_request_state = RepositoryEnumDtoMapper.ToWirePullRequestState(result.State),
            Message = result.Message,
        };
    }

    public static AppMergeRequest ToMergeRequest(WireMergeRequest dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AppMergeRequest(
            Strategy: RepositoryEnumDtoMapper.ToAppMergeStrategy(dto.Strategy),
            DeleteBranch: dto.Delete_branch);
    }
}
