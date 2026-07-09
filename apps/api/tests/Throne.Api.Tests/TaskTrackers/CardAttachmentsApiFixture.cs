using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.TaskTrackers;

/// <summary>
/// Boots the API with an isolated SQLite database and a stub <see cref="ITaskTrackerConnectionProvider"/>
/// registered as the only <c>kaiten</c> tracker so attach/refresh never touch the real HTTP client.
/// Integration-tagged: the <c>intent_card_attachments</c> table lands with the ADR-0052 migration.
/// </summary>
internal sealed class CardAttachmentsApiFixture : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteTestDatabase _database;

    public CardAttachmentsApiFixture(SqliteFixture sqlite)
    {
        ArgumentNullException.ThrowIfNull(sqlite);
        _database = sqlite.CreateDatabase();
        Provider = new StubCardTrackerProvider();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SqliteTestHost.Configure(builder, _database.DataSource);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITaskTrackerProvider>();
                services.AddSingleton<ITaskTrackerProvider>(Provider);
                RemoveBackgroundServiceByName(services, "RepositoryCloneService");
                RemoveBackgroundServiceByName(services, "PullRequestSyncService");
                RemoveBackgroundServiceByName(services, "TaskTrackerSyncService");
            });
        });
        Client = _factory.CreateClient();
    }

    public StubCardTrackerProvider Provider { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services => _factory.Services;

    public async Task<Intent> SeedIntentAsync(DateTimeOffset now)
    {
        await using var scope = Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var intent = Intent.Create(IntentId.New(), "intent-for-card-tests", [TagId.New()], now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, intent.Id.Value,
            intent.State.Text, now, TextVersionAuthor.User);
        var status = IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"), intent.Id, intent.State.CurrentVersion,
            intent.State.Status, intent.State.Status, "test:create", now, IntentTrainingAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, status, Array.Empty<Tag>(), ct), CancellationToken.None);
        return intent;
    }

    public async Task SeedConnectionAsync(string tracker = "kaiten")
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ITaskTrackerConnectionStore>();
        await store.SaveConnectionAsync(tracker, "https://acme.kaiten.ru", "tok", CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static void RemoveBackgroundServiceByName(IServiceCollection services, string typeName)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name == typeName)
            {
                services.RemoveAt(i);
            }
        }
    }
}

/// <summary>
/// Stub <see cref="ITaskTrackerConnectionProvider"/> with a settable card behaviour so the HTTP-level
/// tests drive attach (200 / 404 / 502) and refresh (degrade) without a live tracker.
/// </summary>
internal sealed class StubCardTrackerProvider : ITaskTrackerConnectionProvider
{
    public string TrackerKey => "kaiten";

    public string DisplayName => "Kaiten";

    public Func<string, Task<TaskTrackerCard?>> OnGetCard { get; set; } =
        _ => Task.FromResult<TaskTrackerCard?>(null);

    public Func<string, Task<IReadOnlyList<TaskTrackerCard>>> OnListBoardCards { get; set; } =
        _ => Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

    public Task<TaskTrackerProbeResult> ProbeAsync(TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
        Task.FromResult(TaskTrackerProbeResult.Connected());

    public Task<IReadOnlyList<TaskTrackerSpaceTopology>> ListBoardsAsync(
        TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskTrackerSpaceTopology>>([]);

    public Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection, string boardId, CancellationToken ct) =>
        OnListBoardCards(boardId);

    public Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection, string cardId, CancellationToken ct) =>
        OnGetCard(cardId);

    public static TaskTrackerCard Card(string cardId, string title, bool archived = false) =>
        new(
            CardId: cardId,
            BoardId: "10",
            ColumnId: "100",
            ColumnTitle: "In Progress",
            Title: title,
            Description: "body",
            UpdatedAt: DateTimeOffset.UnixEpoch,
            ColumnChangedAt: DateTimeOffset.UnixEpoch,
            Archived: archived,
            RevisionTag: "v1");
}
