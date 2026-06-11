using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class IntentRepositoryBindingDocumentMapper
{
    public static IntentRepositoryBindingDocument ToDocument(IntentRepositoryBinding binding) => new()
    {
        Id = binding.Id.Value,
        IntentId = binding.IntentId.Value,
        Provider = binding.Coordinate.Provider,
        Host = binding.Coordinate.Host,
        Owner = binding.Coordinate.Owner,
        Repo = binding.Coordinate.Repo,
        ProjectId = binding.Coordinate.ProjectId,
        DefaultBranch = binding.State.DefaultBranch,
        WorkspacePath = binding.WorkspacePath,
        CloneStatus = binding.State.CloneStatus,
        CloneError = binding.State.CloneError,
        PullRequestNumber = binding.State.PullRequestNumber,
        PullRequestState = binding.State.PullRequestState,
        ReviewCommentsEtag = binding.State.ReviewCommentsEtag,
        LastSeenReviewCommentAt = binding.State.LastSeenReviewCommentAt?.UtcDateTime,
        LastSyncedAt = binding.State.LastSyncedAt?.UtcDateTime,
        CreatedAt = binding.CreatedAt.UtcDateTime,
        UpdatedAt = binding.State.UpdatedAt.UtcDateTime,
        SuppressMergeAutoClose = binding.State.SuppressMergeAutoClose,
    };

    public static IntentRepositoryBinding ToDomain(IntentRepositoryBindingDocument doc)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: new BindingId(doc.Id),
            IntentId: new IntentId(doc.IntentId),
            Coordinate: new RepoCoordinate(
                doc.Provider,
                doc.Owner,
                doc.Repo,
                HostOrDefault(doc.Provider, doc.Host),
                doc.ProjectId),
            DefaultBranch: doc.DefaultBranch,
            WorkspacePath: doc.WorkspacePath,
            CloneStatus: doc.CloneStatus,
            CloneError: doc.CloneError,
            PullRequestNumber: doc.PullRequestNumber,
            PullRequestState: doc.PullRequestState,
            ReviewCommentsEtag: doc.ReviewCommentsEtag,
            LastSeenReviewCommentAt: doc.LastSeenReviewCommentAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(doc.LastSeenReviewCommentAt.Value, DateTimeKind.Utc)),
            LastSyncedAt: doc.LastSyncedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(doc.LastSyncedAt.Value, DateTimeKind.Utc)),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
            SuppressMergeAutoClose: doc.SuppressMergeAutoClose);

        return IntentRepositoryBinding.Restore(snapshot);
    }

    private static string? HostOrDefault(string provider, string? host) =>
        string.IsNullOrWhiteSpace(host) && provider == GitProviderNames.GitHub
            ? GitProviderHostDefaults.GitHub
            : host;
}
