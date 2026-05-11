using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record CreateTagCommand(string Name);

public sealed class CreateTagHandler(
    ITagRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Tag> HandleAsync(CreateTagCommand command, CancellationToken ct)
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
            inner => repository.CreateAsync(normalized, now, inner),
            ct);

        return outcome switch
        {
            CreateTagOutcome.Created created => created.Tag,
            CreateTagOutcome.NameTaken taken => throw new ApiException(
                ErrorCodes.TagNameTaken,
                $"Tag '{taken.Existing.Name}' already exists.",
                new Dictionary<string, object?>
                {
                    ["name"] = taken.Existing.Name,
                    ["existing_id"] = taken.Existing.Id.Value,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
