using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

public sealed record GetPromptPartQuery(string PromptPartId);

public sealed class GetPromptPartHandler(IPromptPartRepository repository)
{
    public async Task<PromptPart> HandleAsync(GetPromptPartQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var part = await repository.GetByIdAsync(new PromptPartId(query.PromptPartId), ct);
        return part ?? throw PromptPartErrors.NotFound(query.PromptPartId);
    }
}
