using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Infrastructure.PromptParts;

internal sealed class ManifestBackedPromptPartRepository(
    ISkillManifestProvider manifestProvider,
    IPromptPartRepository store) : IPromptPartRepository
{
    private const string SystemIdPrefix = "system:";
    private static readonly DateTimeOffset SyntheticTimestamp = DateTimeOffset.UnixEpoch;

    public Task<CreatePromptPartOutcome> CreateAsync(
        PromptPart part,
        TextVersion initialVersion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(initialVersion);

        return IsSystemScope(part.Scope)
            ? Task.FromResult<CreatePromptPartOutcome>(new CreatePromptPartOutcome.KeyConflict())
            : store.CreateAsync(part, initialVersion, ct);
    }

    public async Task<IReadOnlyList<PromptPart>> ListAsync(string? scope, CancellationToken ct)
    {
        if (IsSystemScope(scope))
        {
            return ManifestSystemParts();
        }
        if (string.Equals(scope, PromptPartScopeNames.User, StringComparison.Ordinal))
        {
            return await store.ListAsync(PromptPartScopeNames.User, ct);
        }
        if (scope is not null)
        {
            return await store.ListAsync(scope, ct);
        }

        var system = ManifestSystemParts();
        var user = await store.ListAsync(PromptPartScopeNames.User, ct);
        return system.Concat(user).ToArray();
    }

    public async Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct)
    {
        if (TryGetSystemKey(id, out var key))
        {
            return ManifestSystemPartByKey(key);
        }

        var part = await store.GetByIdAsync(id, ct);
        return IsSystemScope(part?.Scope) ? null : part;
    }

    public Task<PromptPart?> GetByScopeKeyAsync(string scope, string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return IsSystemScope(scope)
            ? Task.FromResult(ManifestSystemPartByKey(key))
            : store.GetByScopeKeyAsync(scope, key, ct);
    }

    public Task<IReadOnlyList<PromptPart>> GetByScopeAndKeysAsync(
        string scope,
        IReadOnlyList<string> keys,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(keys);

        if (!IsSystemScope(scope))
        {
            return store.GetByScopeAndKeysAsync(scope, keys, ct);
        }

        var parts = keys
            .Select(ManifestSystemPartByKey)
            .Where(p => p is not null)
            .OrderBy(p => p!.Key, StringComparer.Ordinal)
            .Cast<PromptPart>()
            .ToArray();
        return Task.FromResult<IReadOnlyList<PromptPart>>(parts);
    }

    public Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct) =>
        IsSystemId(id)
            ? Task.FromResult<ReplacePromptPartTextOutcome>(new ReplacePromptPartTextOutcome.NotFound())
            : store.ReplaceTextAsync(id, expectedVersion, oldText, newText, changedBy, now, ct);

    public Task<PromptPart?> SetModeRolesAsync(
        PromptPartId id,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now,
        CancellationToken ct) =>
        IsSystemId(id)
            ? Task.FromResult<PromptPart?>(null)
            : store.SetModeRolesAsync(id, modeRoles, now, ct);

    public Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct) =>
        IsSystemId(id)
            ? Task.FromResult<DeletePromptPartOutcome>(new DeletePromptPartOutcome.NotFound())
            : store.DeleteAsync(id, ct);

    private PromptPart[] ManifestSystemParts()
    {
        var manifest = manifestProvider.Current;
        return manifest.SystemInstructions
            .OrderBy(e => e.Kind, StringComparer.Ordinal)
            .Select(e => ToSystemPart(e, manifest))
            .ToArray();
    }

    private PromptPart? ManifestSystemPartByKey(string key)
    {
        var manifest = manifestProvider.Current;
        var entry = manifest.SystemInstructions.FirstOrDefault(e =>
            string.Equals(e.Kind, key, StringComparison.Ordinal));
        return entry is null ? null : ToSystemPart(entry, manifest);
    }

    private static PromptPart ToSystemPart(SystemInstructionEntry entry, SkillManifest manifest)
    {
        var roles = PromptPartManifestRoles.MandatoryRolesFor(
            PromptPartScopeNames.System,
            entry.Kind,
            manifest);
        return PromptPart.Restore(
            id: new PromptPartId($"{SystemIdPrefix}{entry.Kind}"),
            scope: PromptPartScopeNames.System,
            key: entry.Kind,
            text: entry.Text,
            description: null,
            currentVersion: 1,
            modeRoles: roles,
            createdAt: SyntheticTimestamp,
            updatedAt: SyntheticTimestamp);
    }

    private static bool IsSystemScope(string? scope) =>
        string.Equals(scope, PromptPartScopeNames.System, StringComparison.Ordinal);

    private static bool IsSystemId(PromptPartId id) =>
        id.Value.StartsWith(SystemIdPrefix, StringComparison.Ordinal);

    private static bool TryGetSystemKey(PromptPartId id, out string key)
    {
        if (IsSystemId(id))
        {
            key = id.Value[SystemIdPrefix.Length..];
            return true;
        }

        key = string.Empty;
        return false;
    }
}
