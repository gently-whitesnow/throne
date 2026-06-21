using FluentAssertions;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.PromptParts;

namespace Throne.Infrastructure.Tests.PromptParts;

public class ManifestBackedPromptPartRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "List возвращает system из манифеста и user из store, скрывая orphan Mongo-system")]
    public async Task List_merges_manifest_system_with_store_user_only()
    {
        var store = new RecordingPromptPartRepository(
        [
            UserPart("common", "user common"),
            SystemPart("orphan", "stale mongo system"),
        ]);
        var repo = NewRepository(Manifest(
            ("interview", "manifest interview"),
            ("work", "manifest work")), store);

        var parts = await repo.ListAsync(null, CancellationToken.None);

        parts.Select(p => (p.Scope, p.Key, p.Text)).Should().Equal(
            (PromptPartScopeNames.System, "interview", "manifest interview"),
            (PromptPartScopeNames.System, "work", "manifest work"),
            (PromptPartScopeNames.User, "common", "user common"));
        store.ListScopes.Should().Equal(PromptPartScopeNames.User);
    }

    [Fact(DisplayName = "Get system по scope/key и deterministic id читает манифест, не store")]
    public async Task Get_system_part_reads_manifest_without_store_lookup()
    {
        var store = new RecordingPromptPartRepository([SystemPart("work", "stale mongo work")]);
        var repo = NewRepository(Manifest(("work", "manifest work")), store);

        var byScope = await repo.GetByScopeKeyAsync(
            PromptPartScopeNames.System,
            "work",
            CancellationToken.None);
        var byId = await repo.GetByIdAsync(new PromptPartId("system:work"), CancellationToken.None);

        byScope.Should().NotBeNull();
        byScope!.Id.Value.Should().Be("system:work");
        byScope.Text.Should().Be("manifest work");
        byId.Should().NotBeNull();
        byId!.Text.Should().Be("manifest work");
        store.ScopeKeyLookups.Should().BeEmpty();
        store.IdLookups.Should().BeEmpty();
    }

    [Fact(DisplayName = "PromptCompositionResolver получает mandatory system из манифеста через repository")]
    public async Task Resolver_composes_system_from_manifest()
    {
        var manifest = new SkillManifest(
            Version: 1,
            SystemInstructions: [new SystemInstructionEntry("work", "manifest work")],
            Bundles:
            [
                new BundleDefinition(PromptPartModeNames.Work,
                [
                    new BundleInclude(PromptPartScopeNames.System, "work"),
                    new BundleInclude(PromptPartScopeNames.User, "common"),
                    new BundleInclude(PromptPartScopeNames.User, "missing"),
                ]),
            ],
            DreamSources: []);
        var store = new RecordingPromptPartRepository([UserPart("common", "user common")]);
        var repo = NewRepository(manifest, store);
        var resolver = new PromptCompositionResolver(new InMemorySkillManifestProvider(manifest), repo);

        var composition = await resolver.ResolveAsync(
            new ResolvePromptCompositionQuery(PromptPartModeNames.Work, null, "intent"),
            CancellationToken.None);

        composition.Parts.Select(p => (p.Scope, p.Key)).Should().Equal(
            (PromptPartScopeNames.System, "work"),
            (PromptPartScopeNames.User, "common"));
        composition.SystemPrompt.Should().Be("manifest work\n\nuser common");
        store.ScopeKeyLookups.Should().OnlyContain(x => x.Scope == PromptPartScopeNames.User);
    }

    [Fact(DisplayName = "Write-операции по synthetic system id не делегируются в store")]
    public async Task System_write_operations_are_not_delegated()
    {
        var store = new RecordingPromptPartRepository([]);
        var repo = NewRepository(Manifest(("work", "manifest work")), store);
        var id = new PromptPartId("system:work");

        var replace = await repo.ReplaceTextAsync(
            id,
            expectedVersion: 1,
            oldText: "manifest",
            newText: "edited",
            changedBy: TextVersionAuthor.User,
            now: Now,
            CancellationToken.None);
        var roles = await repo.SetModeRolesAsync(id, [], Now, CancellationToken.None);
        var delete = await repo.DeleteAsync(id, CancellationToken.None);

        replace.Should().BeOfType<ReplacePromptPartTextOutcome.NotFound>();
        roles.Should().BeNull();
        delete.Should().BeOfType<DeletePromptPartOutcome.NotFound>();
        store.WriteCalls.Should().Be(0);
    }

    private static ManifestBackedPromptPartRepository NewRepository(
        SkillManifest manifest,
        RecordingPromptPartRepository store) =>
        new(new InMemorySkillManifestProvider(manifest), store);

    private static SkillManifest Manifest(params (string Kind, string Text)[] system) =>
        new(
            Version: 1,
            SystemInstructions: system.Select(s => new SystemInstructionEntry(s.Kind, s.Text)).ToArray(),
            Bundles:
            [
                new BundleDefinition(PromptPartModeNames.Work,
                [
                    new BundleInclude(PromptPartScopeNames.System, "interview"),
                    new BundleInclude(PromptPartScopeNames.System, "work"),
                ]),
            ],
            DreamSources: []);

    private static PromptPart UserPart(string key, string text) =>
        PromptPart.Create(PromptPartId.New(), PromptPartScopeNames.User, key, text, null, [], Now);

    private static PromptPart SystemPart(string key, string text) =>
        PromptPart.Create(
            new PromptPartId($"system:{key}"),
            PromptPartScopeNames.System,
            key,
            text,
            null,
            [],
            Now);

    private sealed class RecordingPromptPartRepository(IReadOnlyList<PromptPart> parts) : IPromptPartRepository
    {
        public List<string?> ListScopes { get; } = [];
        public List<PromptPartId> IdLookups { get; } = [];
        public List<(string Scope, string Key)> ScopeKeyLookups { get; } = [];
        public int WriteCalls { get; private set; }

        public Task<CreatePromptPartOutcome> CreateAsync(
            PromptPart part,
            TextVersion initialVersion,
            CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult<CreatePromptPartOutcome>(new CreatePromptPartOutcome.Created(part));
        }

        public Task<IReadOnlyList<PromptPart>> ListAsync(string? scope, CancellationToken ct)
        {
            ListScopes.Add(scope);
            var result = parts
                .Where(p => scope is null || string.Equals(p.Scope, scope, StringComparison.Ordinal))
                .OrderBy(p => p.Scope, StringComparer.Ordinal)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<PromptPart>>(result);
        }

        public Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct)
        {
            IdLookups.Add(id);
            return Task.FromResult(parts.FirstOrDefault(p => p.Id == id));
        }

        public Task<PromptPart?> GetByScopeKeyAsync(string scope, string key, CancellationToken ct)
        {
            ScopeKeyLookups.Add((scope, key));
            return Task.FromResult(parts.FirstOrDefault(p =>
                string.Equals(p.Scope, scope, StringComparison.Ordinal)
                && string.Equals(p.Key, key, StringComparison.Ordinal)));
        }

        public Task<IReadOnlyList<PromptPart>> GetByScopeAndKeysAsync(
            string scope,
            IReadOnlyList<string> keys,
            CancellationToken ct)
        {
            var result = parts
                .Where(p => string.Equals(p.Scope, scope, StringComparison.Ordinal) && keys.Contains(p.Key))
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<PromptPart>>(result);
        }

        public Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
            PromptPartId id,
            int expectedVersion,
            string oldText,
            string newText,
            TextVersionAuthor changedBy,
            DateTimeOffset now,
            CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult<ReplacePromptPartTextOutcome>(new ReplacePromptPartTextOutcome.NotFound());
        }

        public Task<PromptPart?> SetModeRolesAsync(
            PromptPartId id,
            IReadOnlyList<PromptPartModeRole> modeRoles,
            DateTimeOffset now,
            CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult<PromptPart?>(null);
        }

        public Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult<DeletePromptPartOutcome>(new DeletePromptPartOutcome.NotFound());
        }
    }
}
