using FluentAssertions;
using Throne.Application.Ports;
using Throne.Domain.Dreams;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCoreDreamSessionRepositoryTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет DreamSession; GetAsync возвращает её")]
    public async Task Create_persists_and_reads_back()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var session = MakeSession("d-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(session, ct), CancellationToken.None);

        var loaded = await repo.GetAsync("d-1", CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Payload.Vendor.Should().Be("claude-code");
        loaded.Payload.Host.Should().Be("host-a.local");
        loaded.Payload.Summary.Should().Be("hello");
        loaded.Payload.ProcessedConversationIds.Should().Equal("a", "b");
    }

    [Fact(DisplayName = "ListAsync host-фильтр отсекает сессии других машин")]
    public async Task List_host_filter_isolates_machines()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var hostA = MakeSession("d-host-a", host: "host-a.local", at: Now);
        var hostB = MakeSession("d-host-b", host: "host-b.local", at: Now.AddMinutes(5));
        await uow.ExecuteAsync(ct => repo.CreateAsync(hostA, ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => repo.CreateAsync(hostB, ct), CancellationToken.None);

        var onlyHostA = await repo.ListAsync(
            new DreamSessionListFilter(Vendor: null, Host: "host-a.local"),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        onlyHostA.Items.Should().ContainSingle().Which.Id.Should().Be("d-host-a");

        var all = await repo.ListAsync(
            new DreamSessionListFilter(Vendor: null),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        all.Items.Should().HaveCount(2);
    }

    [Fact(DisplayName = "ListAsync vendor-фильтр + сортировка по created_at DESC")]
    public async Task List_vendor_filter_sorts_desc()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var older = MakeSession("d-1", vendor: "claude-code", at: Now);
        var newer = MakeSession("d-2", vendor: "codex-cli", at: Now.AddHours(1));
        await uow.ExecuteAsync(ct => repo.CreateAsync(older, ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => repo.CreateAsync(newer, ct), CancellationToken.None);

        var all = await repo.ListAsync(
            new DreamSessionListFilter(Vendor: null),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        all.Items.Should().HaveCount(2);
        all.Items[0].Id.Should().Be("d-2");
        all.Items[1].Id.Should().Be("d-1");

        var onlyCodex = await repo.ListAsync(
            new DreamSessionListFilter(Vendor: "codex-cli"),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        onlyCodex.Items.Should().HaveCount(1);
        onlyCodex.Items[0].Id.Should().Be("d-2");
    }

    private static DreamSession MakeSession(
        string id,
        string vendor = "claude-code",
        string host = "host-a.local",
        DateTimeOffset? at = null) =>
        DreamSession.Create(
            id: id,
            createdAt: at ?? Now,
            vendor: vendor,
            host: host,
            dateFrom: null,
            dateTo: null,
            processedConversationIds: ["a", "b"],
            summary: "hello",
            reflection: null,
            proposedPatchIds: []);

    private async Task<(SqliteTestDatabase Db, IDreamSessionRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<IDreamSessionRepository>(), db.GetRequiredService<IUnitOfWork>());
    }
}
