using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct);

    Task<Tag?> GetByIdAsync(TagId id, CancellationToken ct);

    Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct);

    Task<EnsureTagOutcome> EnsureByNameAsync(string normalizedName, DateTimeOffset now, CancellationToken ct);

    Task<CreateTagOutcome> CreateAsync(string rawName, DateTimeOffset now, CancellationToken ct);

    Task<RenameTagOutcome> RenameAsync(TagId id, int expectedVersion, string rawName, DateTimeOffset now, CancellationToken ct);

    Task<TagUsage> GetUsageAsync(TagId id, CancellationToken ct);

    /// <summary>
    /// Detach the tag from every intent currently referencing it and return the affected intents
    /// (with their post-detach state) plus the deleted tag. Used by DeleteTagHandler when the
    /// caller confirmed detach=true. Must run inside IUnitOfWork.ExecuteAsync.
    /// </summary>
    Task<DeleteTagOutcome> DeleteAsync(TagId id, DateTimeOffset now, CancellationToken ct);
}

public sealed record TagUsage(int IntentsCount);
