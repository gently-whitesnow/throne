using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore;
using Throne.Infrastructure.PromptParts;

namespace Throne.Infrastructure.Tests.PromptParts;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class UserPromptSeedSeederTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserPromptSeed Seed = new(1,
    [
        new UserPromptSeedPart(
            "common", "common text", "общая",
            [new PromptPartModeRole("work", PromptPartRoleNames.Mandatory, 1)]),
        new UserPromptSeedPart(
            "commit", "commit example", null,
            [new PromptPartModeRole("work", PromptPartRoleNames.DefaultOff, 11)]),
    ]);

    [Fact(DisplayName = "На пустой БД сид пишет все части с текстом, ролями и v1-снапшотом")]
    public async Task Seeds_on_empty_database()
    {
        var (store, seeder) = await NewSeederAsync(Seed);

        await seeder.RunAsync(CancellationToken.None);

        var parts = await store.ListAsync(PromptPartScopeNames.User, CancellationToken.None);
        parts.Select(p => p.Key).Should().Equal("commit", "common");
        var common = parts.Single(p => p.Key == "common");
        common.Text.Should().Be("common text");
        common.CurrentVersion.Should().Be(1);
        common.ModeRoles.Single().Role.Should().Be(PromptPartRoleNames.Mandatory);
    }

    [Fact(DisplayName = "Повторный запуск не дублирует и не доливает — идемпотентно")]
    public async Task Rerun_is_idempotent()
    {
        var (store, seeder) = await NewSeederAsync(Seed);

        await seeder.RunAsync(CancellationToken.None);
        await seeder.RunAsync(CancellationToken.None);

        var parts = await store.ListAsync(PromptPartScopeNames.User, CancellationToken.None);
        parts.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Есть хоть одна user-часть → сид не трогает БД (не воскрешает и не доливает)")]
    public async Task No_op_when_any_user_part_exists()
    {
        var db = await fixture.CreateDatabaseAsync();
        var store = db.GetRequiredService<EfPromptPartRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        // Pre-existing user part with a key NOT in the seed: a running instance the operator
        // already shaped. The seed must not top up the missing core/module parts.
        await uow.ExecuteAsync(ct => CreatePartAsync(store, "operator-only", ct), CancellationToken.None);

        var seeder = NewSeeder(db, Seed);
        await seeder.RunAsync(CancellationToken.None);

        var parts = await store.ListAsync(PromptPartScopeNames.User, CancellationToken.None);
        parts.Select(p => p.Key).Should().Equal("operator-only");
    }

    private static Task<CreatePromptPartOutcome> CreatePartAsync(
        EfPromptPartRepository store, string key, CancellationToken ct)
    {
        var part = PromptPart.Create(
            PromptPartId.New(), PromptPartScopeNames.User, key, "text", null,
            [new PromptPartModeRole("work", PromptPartRoleNames.DefaultOff, 0)], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.PromptPart, part.Id.Value,
            part.Text, Now, TextVersionAuthor.User);
        return store.CreateAsync(part, version, ct);
    }

    private async Task<(EfPromptPartRepository Store, UserPromptSeedSeeder Seeder)> NewSeederAsync(UserPromptSeed seed)
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db.GetRequiredService<EfPromptPartRepository>(), NewSeeder(db, seed));
    }

    private static UserPromptSeedSeeder NewSeeder(SqliteTestDatabase db, UserPromptSeed seed) =>
        new(
            new InMemoryUserPromptSeedProvider(seed),
            db.GetRequiredService<EfPromptPartRepository>(),
            db.GetRequiredService<IUnitOfWork>(),
            TimeProvider.System,
            NullLogger<UserPromptSeedSeeder>.Instance);
}
