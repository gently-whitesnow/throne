using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface IPromptPartRepository
{
    /// <summary>
    /// Insert a part together with its v1 snapshot text-version, transactionally.
    /// Must run inside <see cref="IUnitOfWork.ExecuteAsync"/>.
    /// </summary>
    Task<CreatePromptPartOutcome> CreateAsync(PromptPart part, TextVersion initialVersion, CancellationToken ct);

    /// <summary>
    /// Lists prompt parts ordered by scope then key. When <paramref name="scope"/> is null,
    /// returns parts across all scopes (system + user); otherwise filters to that scope.
    /// </summary>
    Task<IReadOnlyList<PromptPart>> ListAsync(string? scope, CancellationToken ct);

    Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct);

    /// <summary>Single part by its (scope, key) identity, or null when absent.</summary>
    Task<PromptPart?> GetByScopeKeyAsync(string scope, string key, CancellationToken ct);

    /// <summary>Batch lookup of parts within a scope by key (bundle resolver / patch lookup).</summary>
    Task<IReadOnlyList<PromptPart>> GetByScopeAndKeysAsync(
        string scope,
        IReadOnlyList<string> keys,
        CancellationToken ct);

    /// <summary>
    /// Replace a unique substring of the part text and append a text-version, transactionally.
    /// </summary>
    Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct);

    Task<PromptPart?> SetModeRolesAsync(
        PromptPartId id,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now,
        CancellationToken ct);

    Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct);
}
