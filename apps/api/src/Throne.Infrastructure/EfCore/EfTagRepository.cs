using Throne.Application.Ports;
using Throne.Application.Tags;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Tags;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Composite EF Core repository that fronts <see cref="ITagRepository"/>. All actual work
/// is delegated to per-concern services in <c>Tags/</c> so each file stays well under the
/// per-type budget and mirrors the Mongo decomposition.
/// </summary>
internal sealed class EfTagRepository(
    EfTagLifecycle lifecycle,
    EfTagListReader listReader,
    EfTagDeletion deletion)
    : ITagRepository
{
    public Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct) =>
        lifecycle.ListAllAsync(ct);

    public Task<TagListPage> ListPageAsync(TagListSpec spec, CancellationToken ct) =>
        listReader.ListPageAsync(spec, ct);

    public Task<Tag?> GetByIdAsync(TagId id, CancellationToken ct) =>
        lifecycle.GetByIdAsync(id, ct);

    public Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct) =>
        lifecycle.FindByNameAsync(normalizedName, ct);

    public Task<EnsureTagOutcome> EnsureByNameAsync(string normalizedName, DateTimeOffset now, CancellationToken ct) =>
        lifecycle.EnsureByNameAsync(normalizedName, now, ct);

    public Task<CreateTagOutcome> CreateAsync(string rawName, DateTimeOffset now, CancellationToken ct) =>
        lifecycle.CreateAsync(rawName, now, ct);

    public Task<RenameTagOutcome> RenameAsync(
        TagId id,
        int expectedVersion,
        string rawName,
        DateTimeOffset now,
        CancellationToken ct) =>
        lifecycle.RenameAsync(id, expectedVersion, rawName, now, ct);

    public Task<SetTagDefaultRepositoriesOutcome> SetDefaultRepositoriesAsync(
        TagId id,
        int expectedVersion,
        IReadOnlyList<TagDefaultRepository> defaultRepositories,
        DateTimeOffset now,
        CancellationToken ct) =>
        lifecycle.SetDefaultRepositoriesAsync(id, expectedVersion, defaultRepositories, now, ct);

    public Task<int> CountAttachedIntentsAsync(TagId id, CancellationToken ct) =>
        deletion.CountAttachedIntentsAsync(id, ct);

    public Task<DeleteTagOutcome> DeleteAsync(TagId id, DateTimeOffset now, CancellationToken ct) =>
        deletion.DeleteAsync(id, now, ct);
}
