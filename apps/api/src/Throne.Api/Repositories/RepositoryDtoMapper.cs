using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Trampoline-level mappers for the repositories HTTP module. Per-aggregate
/// mappers (<see cref="RepositoryBindingDtoMapper"/>, <see cref="PullRequestCommentDtoMapper"/>)
/// own the field-by-field projection; this façade exposes a single entry-point
/// per response shape so endpoint code reads naturally.
/// </summary>
internal static class RepositoryDtoMapper
{
    public static RepositoryBindingDto ToBindingDto(IntentRepositoryBinding binding) =>
        RepositoryBindingDtoMapper.ToDto(binding);

    public static GitBranchRefDto ToBranchRefDto(GitBranchRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new GitBranchRefDto
        {
            Name = reference.Name,
            Is_default = reference.IsDefault,
        };
    }

    public static GitPullRequestRefDto ToPullRequestRefDto(GitPullRequestRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new GitPullRequestRefDto
        {
            Number = reference.Number,
            Title = reference.Title,
            Head_ref = reference.HeadRef,
            State = RepositoryEnumDtoMapper.ToWirePullRequestState(reference.State),
        };
    }

    public static GitRepositoryRefDto ToRepositoryRefDto(GitRepositoryRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new GitRepositoryRefDto
        {
            Provider = RepositoryEnumDtoMapper.ToWireProvider(reference.Provider),
            Owner = reference.Owner,
            Repo = reference.Repo,
            Full_name = reference.FullName,
            Default_branch = reference.DefaultBranch,
            Description = reference.Description,
            Private = reference.Private,
            Html_url = PullRequestCommentDtoMapper.ToUri(reference.HtmlUrl),
        };
    }

    public static PullRequestCommentDto ToCommentDto(PullRequestCommentRecord record) =>
        PullRequestCommentDtoMapper.ToDto(record);

    public static PullRequestSyncResultDto ToSyncResultDto(SyncRepositoryPullRequestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var binding = result.Binding;
        var dto = new PullRequestSyncResultDto
        {
            Binding_id = binding.Id.Value,
            New_comments = result.NewComments.Count,
            Total_comments = result.AllStored.Count,
            Pull_request_state = RepositoryBindingDtoMapper.WirePullRequestState(binding.State.PullRequestState),
            Last_synced_at = binding.State.LastSyncedAt,
        };
        foreach (var record in result.AllStored)
        {
            dto.Comments.Add(PullRequestCommentDtoMapper.ToDto(record));
        }
        return dto;
    }
}
