using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface ITextVersionRepository
{
    Task<IReadOnlyList<TextVersion>> ListByOwnerAsync(
        TextVersionOwnerKind ownerKind,
        string ownerId,
        CancellationToken ct);
}
