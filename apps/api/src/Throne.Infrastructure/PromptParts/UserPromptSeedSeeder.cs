using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore;

namespace Throne.Infrastructure.PromptParts;

/// <summary>
/// First-run seed of editable user-scope prompt parts from the user-seed manifest
/// (ADR-0051). Idempotent by emptiness: it writes the starter set only when
/// <c>prompt_parts(scope=user)</c> is completely empty (a truly first boot). Any existing
/// user part makes it a no-op — it never resurrects deleted parts and never tops up running
/// instances. Writes go through the real EF store (<see cref="EfPromptPartRepository"/>),
/// not the manifest-backed decorator, so the rows land as real editable user data.
/// </summary>
internal sealed partial class UserPromptSeedSeeder(
    IUserPromptSeedProvider seedProvider,
    EfPromptPartRepository store,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<UserPromptSeedSeeder> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal Task RunAsync(CancellationToken ct) =>
        unitOfWork.ExecuteAsync(inner => SeedIfEmptyAsync(inner), ct);

    private async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        var existing = await store.ListAsync(PromptPartScopeNames.User, ct);
        if (existing.Count > 0)
        {
            LogSkipped(log, existing.Count);
            return;
        }

        var seeded = 0;
        foreach (var part in seedProvider.Current.Parts)
        {
            var now = clock.GetUtcNow();
            var entity = PromptPart.Create(
                id: PromptPartId.New(),
                scope: PromptPartScopeNames.User,
                key: part.Key,
                text: part.Text,
                description: part.Description,
                modeRoles: part.ModeRoles,
                now: now);
            var version = TextVersion.CreateSnapshot(
                id: Guid.NewGuid().ToString("N"),
                ownerKind: TextVersionOwnerKind.PromptPart,
                ownerId: entity.Id.Value,
                snapshot: entity.Text,
                changedAt: now,
                changedBy: TextVersionAuthor.System);

            await store.CreateAsync(entity, version, ct);
            seeded++;
        }

        LogSeeded(log, seeded);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "UserPromptSeedSeeder: seeded {Count} starter user prompt part(s).")]
    private static partial void LogSeeded(ILogger logger, int count);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "UserPromptSeedSeeder: skipped, {Count} user prompt part(s) already present.")]
    private static partial void LogSkipped(ILogger logger, int count);
}
