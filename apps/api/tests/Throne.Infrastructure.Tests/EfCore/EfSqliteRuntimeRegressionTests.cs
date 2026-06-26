using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore;
using Throne.Infrastructure.EfCore.Intents;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.EfCore;

public sealed class EfSqliteRuntimeRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"throne-ef-runtime-{Guid.NewGuid():N}");

    private string DbPath => Path.Combine(_root, "throne.db");

    [Fact(DisplayName = "ListPagedAsync на sqlite фильтрует tag/untagged и проходит cursor round-trip")]
    public async Task Intent_list_filters_tags_and_paginates_with_sqlite()
    {
        using var provider = await BuildMigratedProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ThroneDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var tag = TagId.New();

        await SeedIntentsAsync(
            factory,
            new IntentRow
            {
                Id = "a",
                Text = "tagged first",
                Status = IntentStatusNames.Draft,
                CurrentVersion = 1,
                TagIds = [tag.Value],
                SortKey = "a",
                CreatedAt = now.AddMinutes(-3),
                UpdatedAt = now.AddMinutes(-3),
            },
            new IntentRow
            {
                Id = "b",
                Text = "untagged second",
                Status = IntentStatusNames.Draft,
                CurrentVersion = 1,
                TagIds = [],
                SortKey = "b",
                CreatedAt = now.AddMinutes(-2),
                UpdatedAt = now.AddMinutes(-2),
            },
            new IntentRow
            {
                Id = "c",
                Text = "tagged third",
                Status = IntentStatusNames.Draft,
                CurrentVersion = 1,
                TagIds = [tag.Value],
                SortKey = "c",
                CreatedAt = now.AddMinutes(-1),
                UpdatedAt = now.AddMinutes(-1),
            });

        var reader = new EfIntentReader(factory, new EfSessionAccessor());
        var tagged = await reader.ListPagedAsync(
            new IntentListSpec(null, tag, false, false, null, IntentListSort.CreatedAsc, 10, null),
            CancellationToken.None);
        var untagged = await reader.ListPagedAsync(
            new IntentListSpec(null, null, true, false, null, IntentListSort.CreatedAsc, 10, null),
            CancellationToken.None);

        var firstPage = await reader.ListPagedAsync(
            new IntentListSpec(null, null, false, false, null, IntentListSort.SortKeyAsc, 1, null),
            CancellationToken.None);
        var secondPage = await reader.ListPagedAsync(
            new IntentListSpec(null, null, false, false, null, IntentListSort.SortKeyAsc, 1, firstPage.NextCursor),
            CancellationToken.None);

        tagged.Items.Select(i => i.Id.Value).Should().BeEquivalentTo(["a", "c"]);
        untagged.Items.Select(i => i.Id.Value).Should().Equal("b");
        firstPage.NextCursor.Should().NotBeNull();
        firstPage.Items.Select(i => i.Id.Value).Should().Equal("a");
        secondPage.Items.Select(i => i.Id.Value).Should().Equal("b");
    }

    [Fact(DisplayName = "GetContextCountsAsync на sqlite суммирует legacy empty status как draft")]
    public async Task Context_counts_treat_legacy_empty_status_as_draft()
    {
        using var provider = await BuildMigratedProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ThroneDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var tag = TagId.New();

        await SeedIntentsAsync(
            factory,
            new IntentRow
            {
                Id = "legacy-empty-status",
                Text = "legacy",
                Status = string.Empty,
                CurrentVersion = 1,
                TagIds = [],
                SortKey = "a",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new IntentRow
            {
                Id = "normal-draft",
                Text = "draft",
                Status = IntentStatusNames.Draft,
                CurrentVersion = 1,
                TagIds = [tag.Value],
                SortKey = "b",
                CreatedAt = now,
                UpdatedAt = now,
            });

        var reader = new EfIntentContextReader(factory, new EfSessionAccessor());
        var counts = await reader.GetContextCountsAsync([], CancellationToken.None);

        counts.Untagged.Should().Be(1);
        counts.Tags.Should().ContainSingle(t => t.TagId == tag.Value && t.Count == 1);
    }

    [Fact(DisplayName = "Attachment cleanup/compression на sqlite работают без ambient UoW")]
    public async Task Attachment_cleanup_and_compression_work_without_ambient_unit_of_work()
    {
        using var provider = await BuildMigratedProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ThroneDbContext>>();
        var now = DateTimeOffset.UtcNow;

        await using (var context = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            context.Set<IntentAttachmentRow>().AddRange(
                new IntentAttachmentRow
                {
                    Id = "delete-me",
                    IntentId = "intent-delete",
                    FileName = "delete.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 3,
                    CreatedAt = now,
                    CompressionState = IntentAttachmentRowMapper.CompressionStatePending,
                    ContentBytes = [1, 2, 3],
                },
                new IntentAttachmentRow
                {
                    Id = "compress-me",
                    IntentId = "intent-compress",
                    FileName = "compress.png",
                    ContentType = "image/png",
                    SizeBytes = 3,
                    CreatedAt = now,
                    CompressionState = null,
                    ContentBytes = [1, 2, 3],
                });
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var repo = new EfIntentAttachmentRepository(factory, new EfSessionAccessor());

        await repo.DeleteAllForIntentAsync(new IntentId("intent-delete"), CancellationToken.None);
        await repo.ApplyCompressionAsync(
            "compress-me",
            "compress-me",
            new DownscaledImage([9, 8], "image/jpeg", 10, 20),
            CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync(CancellationToken.None);
        (await verify.Set<IntentAttachmentRow>().AnyAsync(r => r.Id == "delete-me", CancellationToken.None))
            .Should().BeFalse();

        var compressed = await verify.Set<IntentAttachmentRow>().SingleAsync(r => r.Id == "compress-me");
        compressed.CompressionState.Should().Be(IntentAttachmentRowMapper.CompressionStateReady);
        compressed.ContentType.Should().Be("image/jpeg");
        compressed.SizeBytes.Should().Be(2);
        compressed.DerivedWidth.Should().Be(10);
        compressed.DerivedHeight.Should().Be(20);
        compressed.ContentBytes.Should().Equal([9, 8]);
    }

    private async Task<ServiceProvider> BuildMigratedProviderAsync()
    {
        Directory.CreateDirectory(_root);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PersistenceProvider.Key] = PersistenceProvider.Sqlite,
                [$"{EfPersistenceOptions.SectionName}:DataSource"] = DbPath,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        var skillCatalog = Substitute.For<ISessionSkillCatalog>();
        skillCatalog.List().Returns(System.Array.Empty<SessionSkillDescriptor>());
        services.AddSingleton(skillCatalog);
        services.AddSingleton(Substitute.For<ITerminalVendorCatalog>());
        services.AddThroneEfCore(configuration);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ThroneDbContext>>();
        await using var context = await factory.CreateDbContextAsync(CancellationToken.None);
        await context.Database.MigrateAsync(CancellationToken.None);
        return provider;
    }

    private static async Task SeedIntentsAsync(
        IDbContextFactory<ThroneDbContext> factory,
        params IntentRow[] rows)
    {
        await using var context = await factory.CreateDbContextAsync(CancellationToken.None);
        context.Set<IntentRow>().AddRange(rows);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
