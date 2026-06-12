using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Idempotent startup service (ADR-0036). On every start it:
///   1. SEED — reconciles <c>system</c> prompt parts with the manifest (creates missing as v1,
///      writes a new version on text drift, reconciles mode-roles);
///   2. MIGRATE — one-time import of legacy user <c>instructions</c> into <c>prompt_parts</c>
///      (current text as the first version, mandatory roles from the manifest);
///   3. RETIRE — drops the legacy <c>instructions</c> collection and stale
///      <c>text_versions</c> rows written under owner_kind=instruction.
/// All steps are idempotent and run after <see cref="MongoIndexInitializer"/>.
/// </summary>
internal sealed class PromptPartSeeder(
    IMongoDatabase database,
    ISkillManifestProvider manifestProvider,
    IPromptPartRepository parts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : BackgroundService
{
    private const string LegacyInstructionsCollection = "instructions";
    private const int LegacyInstructionOwnerKind = 2;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);

    /// <summary>Runs the full seed → migrate → retire pass once. Exposed for integration tests.</summary>
    internal async Task RunAsync(CancellationToken ct)
    {
        var manifest = manifestProvider.Current;
        await SeedSystemPartsAsync(manifest, ct);
        await MigrateUserInstructionsAsync(manifest, ct);
        await RetireLegacyAsync(ct);
    }

    private async Task SeedSystemPartsAsync(SkillManifest manifest, CancellationToken ct)
    {
        foreach (var entry in manifest.SystemInstructions)
        {
            var existing = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, entry.Kind, ct);
            if (existing is null)
            {
                await CreateAsync(PromptPartScopeNames.System, entry.Kind, entry.Text, manifest, ct);
                continue;
            }

            await ReconcileSystemPartAsync(existing, entry, manifest, ct);
        }
    }

    private async Task ReconcileSystemPartAsync(
        PromptPart existing, SystemInstructionEntry entry, SkillManifest manifest, CancellationToken ct)
    {
        if (!string.Equals(existing.Text, entry.Text, StringComparison.Ordinal))
        {
            var now = clock.GetUtcNow();
            await unitOfWork.ExecuteAsync(
                inner => parts.ReplaceTextAsync(
                    existing.Id,
                    existing.CurrentVersion,
                    existing.Text,
                    entry.Text,
                    TextVersionAuthor.System,
                    now,
                    inner),
                ct);
        }

        var desiredRoles = PromptPartManifestRoles.MandatoryRolesFor(
            PromptPartScopeNames.System, entry.Kind, manifest);
        if (!RolesEqual(existing.ModeRoles, desiredRoles))
        {
            var now = clock.GetUtcNow();
            await unitOfWork.ExecuteAsync(
                inner => parts.SetModeRolesAsync(existing.Id, desiredRoles, now, inner),
                ct);
        }
    }

    private async Task MigrateUserInstructionsAsync(SkillManifest manifest, CancellationToken ct)
    {
        if (!await CollectionExistsAsync(LegacyInstructionsCollection, ct))
        {
            return;
        }

        var legacy = database.GetCollection<BsonDocument>(LegacyInstructionsCollection);
        var userFilter = Builders<BsonDocument>.Filter.Eq("scope", PromptPartScopeNames.User);
        var docs = await legacy.Find(userFilter).ToListAsync(ct);

        foreach (var doc in docs)
        {
            var key = doc.GetValue("kind", BsonNull.Value).IsString ? doc["kind"].AsString : null;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            if (await parts.GetByScopeKeyAsync(PromptPartScopeNames.User, key, ct) is not null)
            {
                continue;
            }
            var text = doc.GetValue("text", BsonString.Empty).IsString ? doc["text"].AsString : string.Empty;
            await CreateAsync(PromptPartScopeNames.User, key, text, manifest, ct);
        }
    }

    private async Task RetireLegacyAsync(CancellationToken ct)
    {
        if (await CollectionExistsAsync(LegacyInstructionsCollection, ct))
        {
            await database.DropCollectionAsync(LegacyInstructionsCollection, ct);
        }

        var textVersions = database.GetCollection<BsonDocument>(MongoCollectionNames.TextVersions);
        await textVersions.DeleteManyAsync(
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("owner_kind", "instruction"),
                Builders<BsonDocument>.Filter.Eq("owner_kind", LegacyInstructionOwnerKind)),
            ct);
    }

    private async Task CreateAsync(string scope, string key, string text, SkillManifest manifest, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var modeRoles = PromptPartManifestRoles.MandatoryRolesFor(scope, key, manifest);
        var part = PromptPart.Create(PromptPartId.New(), scope, key, text, description: null, modeRoles, now);
        var initialVersion = TextVersion.CreateSnapshot(
            id: Guid.NewGuid().ToString("N"),
            ownerKind: TextVersionOwnerKind.PromptPart,
            ownerId: part.Id.Value,
            snapshot: part.Text,
            changedAt: now,
            changedBy: TextVersionAuthor.System);
        await unitOfWork.ExecuteAsync(inner => parts.CreateAsync(part, initialVersion, inner), ct);
    }

    private async Task<bool> CollectionExistsAsync(string name, CancellationToken ct)
    {
        var filter = new BsonDocument("name", name);
        using var cursor = await database.ListCollectionNamesAsync(
            new ListCollectionNamesOptions { Filter = filter }, ct);
        return await cursor.AnyAsync(ct);
    }

    private static bool RolesEqual(IReadOnlyList<PromptPartModeRole> a, IReadOnlyList<PromptPartModeRole> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        var ordered = a.OrderBy(r => r.Mode, StringComparer.Ordinal).ToList();
        var desired = b.OrderBy(r => r.Mode, StringComparer.Ordinal).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!string.Equals(ordered[i].Mode, desired[i].Mode, StringComparison.Ordinal)
                || !string.Equals(ordered[i].Role, desired[i].Role, StringComparison.Ordinal)
                || ordered[i].Order != desired[i].Order)
            {
                return false;
            }
        }
        return true;
    }
}
