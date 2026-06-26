using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.PromptParts;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Composite EF Core repository that fronts <see cref="IPromptPartRepository"/>.
/// Delegates each concern to a per-file collaborator in <c>PromptParts/</c> so the
/// composite stays a thin façade and each file stays under the per-type budget.
/// The manifest-backed system parts are layered on top of this store by
/// <c>ManifestBackedPromptPartRepository</c> in <see cref="EfCoreModule"/>.
/// </summary>
internal sealed class EfPromptPartRepository(
    EfPromptPartLifecycle lifecycle,
    EfPromptPartTextEditor textEditor)
    : IPromptPartRepository
{
    public Task<CreatePromptPartOutcome> CreateAsync(
        PromptPart part,
        TextVersion initialVersion,
        CancellationToken ct) =>
        lifecycle.CreateAsync(part, initialVersion, ct);

    public Task<IReadOnlyList<PromptPart>> ListAsync(string? scope, CancellationToken ct) =>
        lifecycle.ListAsync(scope, ct);

    public Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct) =>
        lifecycle.GetByIdAsync(id, ct);

    public Task<PromptPart?> GetByScopeKeyAsync(string scope, string key, CancellationToken ct) =>
        lifecycle.GetByScopeKeyAsync(scope, key, ct);

    public Task<IReadOnlyList<PromptPart>> GetByScopeAndKeysAsync(
        string scope,
        IReadOnlyList<string> keys,
        CancellationToken ct) =>
        lifecycle.GetByScopeAndKeysAsync(scope, keys, ct);

    public Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct) =>
        textEditor.ReplaceTextAsync(id, expectedVersion, oldText, newText, changedBy, now, ct);

    public Task<PromptPart?> SetModeRolesAsync(
        PromptPartId id,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now,
        CancellationToken ct) =>
        textEditor.SetModeRolesAsync(id, modeRoles, now, ct);

    public Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct) =>
        lifecycle.DeleteAsync(id, ct);
}
