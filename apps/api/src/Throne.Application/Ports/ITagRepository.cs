using Throne.Application.Tags;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct);

    /// <summary>
    /// One keyset page ordered by <c>usage_count desc, _id asc</c> with an optional
    /// case-insensitive substring filter on the name. Each item carries the
    /// denormalized <c>intents_count</c> inline (no usage round-trip).
    /// </summary>
    Task<TagListPage> ListPageAsync(TagListSpec spec, CancellationToken ct);

    Task<Tag?> GetByIdAsync(TagId id, CancellationToken ct);

    Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct);

    Task<EnsureTagOutcome> EnsureByNameAsync(string normalizedName, DateTimeOffset now, CancellationToken ct);

    Task<CreateTagOutcome> CreateAsync(string rawName, DateTimeOffset now, CancellationToken ct);

    Task<RenameTagOutcome> RenameAsync(TagId id, int expectedVersion, string rawName, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Whole-list replace of the tag's <c>default_repositories</c> (Slice 2). Caller passes
    /// <paramref name="expectedVersion"/> for optimistic concurrency. Dedup on
    /// <c>(provider, owner, repo)</c> is enforced by the aggregate. Must run inside
    /// <see cref="IUnitOfWork.ExecuteAsync(System.Func{System.Threading.CancellationToken, System.Threading.Tasks.Task}, System.Threading.CancellationToken)"/>.
    /// </summary>
    Task<SetTagDefaultRepositoriesOutcome> SetDefaultRepositoriesAsync(
        TagId id,
        int expectedVersion,
        IReadOnlyList<TagDefaultRepository> defaultRepositories,
        DateTimeOffset now,
        CancellationToken ct);

    Task<TagUsage> GetUsageAsync(TagId id, CancellationToken ct);

    /// <summary>
    /// Detach the tag from every intent currently referencing it and return the affected intents
    /// (with their post-detach state) plus the deleted tag. Used by DeleteTagHandler when the
    /// caller confirmed detach=true. Must run inside IUnitOfWork.ExecuteAsync.
    /// </summary>
    Task<DeleteTagOutcome> DeleteAsync(TagId id, DateTimeOffset now, CancellationToken ct);
}

public sealed record TagUsage(int IntentsCount);
