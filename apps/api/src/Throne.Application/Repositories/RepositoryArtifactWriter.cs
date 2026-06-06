using Throne.Application.Errors;
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

        var outcome = await unitOfWork.ExecuteAsync(
            async inner =>
            {
                await registry.EnsureRepositoryAsync(command.Coordinate, now, inner);
                return await artifacts.WriteAsync(
                    command.Coordinate,
                    command.Slug,
                    command.Title,
                    command.Document,
                    command.RenderHint,
                    command.ExpectedVersion,
                    now,
                    inner);
            },
            ct);

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
