using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record DeleteTagCommand(string TagId, bool ConfirmDetach);

public sealed class DeleteTagHandler(
    ITagRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<DeleteTagResult> HandleAsync(DeleteTagCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new TagId(command.TagId);
        var tag = await repository.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{command.TagId}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = command.TagId });

        if (!command.ConfirmDetach)
        {
            var intentsCount = await repository.CountAttachedIntentsAsync(id, ct);
            if (intentsCount > 0)
            {
                throw new ApiException(
                    ErrorCodes.TagInUse,
                    $"Tag '{tag.Name}' is still attached to intent(s). Pass detach=true to confirm.",
                    new Dictionary<string, object?> { ["tag_id"] = command.TagId });
            }
        }

        var now = clock.GetUtcNow();
        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.DeleteAsync(id, now, inner),
            ct);

        return outcome switch
        {
            DeleteTagOutcome.Deleted deleted => new DeleteTagResult(deleted.AffectedIntents.Count),
            DeleteTagOutcome.NotFound => throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{command.TagId}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = command.TagId }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}

public sealed record DeleteTagResult(int IntentsDetached);
