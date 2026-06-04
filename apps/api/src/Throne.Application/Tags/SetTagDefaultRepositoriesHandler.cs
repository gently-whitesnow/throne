using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record SetTagDefaultRepositoriesCommand(
    string TagId,
    int ExpectedVersion,
    IReadOnlyList<TagDefaultRepository> DefaultRepositories);

/// <summary>
/// Whole-list replace for <see cref="Tag.DefaultRepositories"/> (Slice 2). The
/// orchestrator does not race the pre-flight pipeline: existing intent bindings stay
/// in place — only future Run pre-flights observe the new union.
/// </summary>
public sealed class SetTagDefaultRepositoriesHandler(
    ITagRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Tag> HandleAsync(SetTagDefaultRepositoriesCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tagId = new TagId(command.TagId);
        var now = clock.GetUtcNow();
        IReadOnlyList<TagDefaultRepository> domain;
        try
        {
            domain = command.DefaultRepositories;
        }
        catch (ArgumentException ex)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                ex.Message,
                new Dictionary<string, object?> { ["tag_id"] = command.TagId });
        }

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.SetDefaultRepositoriesAsync(
                tagId,
                command.ExpectedVersion,
                domain,
                now,
                inner),
            ct);

        return outcome switch
        {
            SetTagDefaultRepositoriesOutcome.Updated updated => updated.Tag,
            SetTagDefaultRepositoriesOutcome.NoChange noChange => noChange.Tag,
            SetTagDefaultRepositoriesOutcome.NotFound => throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{command.TagId}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = command.TagId }),
            SetTagDefaultRepositoriesOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.TagVersionConflict,
                $"Tag version conflict (current_version={conflict.CurrentVersion}).",
                new Dictionary<string, object?>
                {
                    ["tag_id"] = command.TagId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
