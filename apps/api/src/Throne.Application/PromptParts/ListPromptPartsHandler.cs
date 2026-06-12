using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

public sealed record ListPromptPartsQuery;

public sealed class ListPromptPartsHandler(IPromptPartRepository repository)
{
    public Task<IReadOnlyList<PromptPart>> HandleAsync(ListPromptPartsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return repository.ListAsync(ct);
    }
}
