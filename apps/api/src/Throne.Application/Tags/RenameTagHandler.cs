using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record RenameTagCommand(string TagId, int ExpectedVersion, string Name);

public sealed class RenameTagHandler(
    ITagRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Tag> HandleAsync(RenameTagCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        string normalized;
        try
        {
            normalized = TagNames.Normalize(command.Name);
        }
        catch (ArgumentException ex)
        {
            throw new ApiException(
                ErrorCodes.TagNameInvalid,
                ex.Message,
                new Dictionary<string, object?> { ["name"] = command.Name });
        }

        var now = clock.GetUtcNow();
        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.RenameAsync(new TagId(command.TagId), command.ExpectedVersion, normalized, now, inner),
            ct);

        return outcome switch
        {
            RenameTagOutcome.Renamed renamed => renamed.Tag,
            RenameTagOutcome.NoChange noChange => noChange.Tag,
            RenameTagOutcome.NotFound => throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{command.TagId}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = command.TagId }),
            RenameTagOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.TagVersionConflict,
                $"Tag version conflict (current_version={conflict.CurrentVersion}).",
                new Dictionary<string, object?>
                {
                    ["tag_id"] = command.TagId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),
            RenameTagOutcome.NameTaken taken => throw new ApiException(
                ErrorCodes.TagNameTaken,
                $"Tag name '{taken.Existing.Name}' is already used.",
                new Dictionary<string, object?>
                {
                    ["name"] = taken.Existing.Name,
                    ["existing_id"] = taken.Existing.Id.Value,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
