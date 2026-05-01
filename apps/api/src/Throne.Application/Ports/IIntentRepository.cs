using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface IIntentRepository
{
    Task CreateAsync(Intent intent, TextVersion initialVersion, CancellationToken ct);

    Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct);
}
