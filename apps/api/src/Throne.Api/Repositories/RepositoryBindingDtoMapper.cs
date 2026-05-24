using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Projects a domain <see cref="IntentRepositoryBinding"/> onto the wire
/// <see cref="RepositoryBindingDto"/>. Split out of <see cref="RepositoryDtoMapper"/>
/// so the per-class CA1502 cyclomatic budget holds.
/// </summary>
internal static class RepositoryBindingDtoMapper
{
    public static RepositoryBindingDto ToDto(IntentRepositoryBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var state = binding.State;
        return new RepositoryBindingDto
        {
            Id = binding.Id.Value,
            Intent_id = binding.IntentId.Value,
            Provider = RepositoryEnumDtoMapper.ToWireProvider(binding.Coordinate.Provider),
            Owner = binding.Coordinate.Owner,
            Repo = binding.Coordinate.Repo,
            Default_branch = state.DefaultBranch,
            Workspace_path = binding.WorkspacePath,
            Clone_status = RepositoryEnumDtoMapper.ToWireCloneStatus(state.CloneStatus),
            Clone_error = state.CloneError,
            Pull_request_number = state.PullRequestNumber,
            Pull_request_state = WirePullRequestState(state.PullRequestState),
            Review_comments_etag = state.ReviewCommentsEtag,
            Last_synced_at = state.LastSyncedAt,
            Created_at = binding.CreatedAt,
            Updated_at = state.UpdatedAt,
        };
    }

    public static PullRequestState? WirePullRequestState(string? value) =>
        value is null ? null : RepositoryEnumDtoMapper.ToWirePullRequestState(value);
}
