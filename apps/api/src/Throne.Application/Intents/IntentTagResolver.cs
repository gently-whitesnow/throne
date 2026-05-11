using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Intents;

public sealed record ResolvedTags(IReadOnlyList<TagId> TagIds, IReadOnlyList<Tag> CreatedTags);

/// <summary>
/// Facade combining <see cref="TagNameEnsurer"/> and <see cref="TagIdLookup"/>.
/// Used by CreateIntent (names-only) and SetIntentTags (ids + names).
/// </summary>
public sealed class IntentTagResolver(TagNameEnsurer ensurer, TagIdLookup ids)
{
    public Task<ResolvedTags> EnsureByNamesAsync(
        IReadOnlyList<string>? names, DateTimeOffset now, CancellationToken ct) =>
        ensurer.EnsureAllAsync(names, now, new TagAccumulator(), ct);

    public async Task<ResolvedTags> ResolveAsync(
        IReadOnlyList<string>? tagIds,
        IReadOnlyList<string>? tagNames,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var acc = new TagAccumulator();
        await ids.AddAllAsync(tagIds, acc, ct);
        await ensurer.EnsureAllAsync(tagNames, now, acc, ct);
        return acc.Snapshot();
    }
}

public sealed class TagNameEnsurer(ITagRepository tagRepository)
{
    public async Task<ResolvedTags> EnsureAllAsync(
        IReadOnlyList<string>? names, DateTimeOffset now, TagAccumulator acc, CancellationToken ct)
    {
        if (names is null || names.Count == 0)
        {
            return acc.Snapshot();
        }
        foreach (var raw in names)
        {
            await EnsureOneAsync(raw, acc, now, ct);
        }
        return acc.Snapshot();
    }

    private async Task EnsureOneAsync(string raw, TagAccumulator acc, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }
        var normalized = TagNameNormalizer.NormalizeOrThrow(raw);
        if (!acc.MarkSeen(normalized))
        {
            return;
        }
        var ensure = await tagRepository.EnsureByNameAsync(normalized, now, ct);
        if (!acc.MarkSeen(ensure.Tag.Id.Value))
        {
            return;
        }
        acc.AddId(ensure.Tag.Id);
        if (ensure is EnsureTagOutcome.Created createdOutcome)
        {
            acc.AddCreated(createdOutcome.Value);
        }
    }
}

public sealed class TagIdLookup(ITagRepository tagRepository)
{
    public async Task AddAllAsync(IReadOnlyList<string>? tagIds, TagAccumulator acc, CancellationToken ct)
    {
        if (tagIds is null || tagIds.Count == 0)
        {
            return;
        }
        foreach (var raw in tagIds)
        {
            await AddOneAsync(raw, acc, ct);
        }
    }

    private async Task AddOneAsync(string raw, TagAccumulator acc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || !acc.MarkSeen(raw))
        {
            return;
        }
        var tagId = new TagId(raw);
        _ = await tagRepository.GetByIdAsync(tagId, ct)
            ?? throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{raw}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = raw });
        acc.AddId(tagId);
    }
}

public sealed class TagAccumulator
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly List<TagId> _ids = new();
    private readonly List<Tag> _created = new();

    public bool MarkSeen(string key) => _seen.Add(key);
    public void AddId(TagId id) => _ids.Add(id);
    public void AddCreated(Tag tag) => _created.Add(tag);
    public ResolvedTags Snapshot() => new(_ids, _created);
}

internal static class TagNameNormalizer
{
    public static string NormalizeOrThrow(string raw)
    {
        try
        {
            return TagNames.Normalize(raw);
        }
        catch (ArgumentException ex)
        {
            throw new ApiException(
                ErrorCodes.TagNameInvalid,
                ex.Message,
                new Dictionary<string, object?> { ["name"] = raw });
        }
    }
}
