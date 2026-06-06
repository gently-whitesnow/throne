using Throne.Application.Errors;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record WriteRepositoryArtifactCommand(
    RepoCoordinate Coordinate,
    string Slug,
    string Title,
    string Document,
    string RenderHint,
    int? ExpectedVersion);

/// <summary>
/// Application entry point for writing a <see cref="RepositoryArtifact"/> knowledge page.
/// Runs the registry materialisation (ADR-0031: a page write lazy-upserts its
/// <see cref="Repository"/>) and the artifact upsert in one transaction, and maps the
/// optimistic-concurrency miss to the typed <see cref="ApiException"/> contract used by
/// Intent / Instruction / Tag.
///
/// The MCP / HTTP write surfaces that drive this live in later slices; the port is exercised
/// here and by the persistence tests.
/// </summary>
public sealed class RepositoryArtifactWriter(
    IRepositoryArtifactRepository artifacts,
    IRepositoryRegistry registry,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<RepositoryArtifact> WriteAsync(WriteRepositoryArtifactCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();

        var combined = await unitOfWork.ExecuteAsync(
            async inner =>
            {
                var ensure = await registry.EnsureRepositoryAsync(command.Coordinate, now, inner);
                var write = await artifacts.WriteAsync(
                    command.Coordinate,
                    command.Slug,
                    command.Title,
                    command.Document,
                    command.RenderHint,
                    command.ExpectedVersion,
                    now,
                    inner);
                return new WriteArtifactWithRegistryOutcome(write, ensure);
            },
            ct);

        var outcome = combined.Write;
        return outcome switch
        {
            WriteRepositoryArtifactOutcome.Written written => written.Artifact,
            WriteRepositoryArtifactOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.RepositoryArtifactVersionConflict,
                $"Repository artifact version conflict (current_version={conflict.CurrentVersion}).",
                new Dictionary<string, object?>
                {
                    ["provider"] = command.Coordinate.Provider,
                    ["owner"] = command.Coordinate.Owner,
                    ["repo"] = command.Coordinate.Repo,
                    ["slug"] = command.Slug,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}

/// <summary>
/// Surfaces both events from a page write that also materialised the registry: the page's
/// <c>repository.document_updated</c> and, on first appearance of the coordinate, the registry's
/// <c>repository.registered</c>. The dispatching unit-of-work decorator drains this aggregate
/// after a successful commit (same shape as <c>SetIntentTagsHandlerOutcome</c>).
/// </summary>
internal sealed record WriteArtifactWithRegistryOutcome(
    WriteRepositoryArtifactOutcome Write,
    EnsureRepositoryOutcome Registry) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events =>
        Registry.Events.Count == 0
            ? Write.Events
            : [.. Write.Events, .. Registry.Events];
}
