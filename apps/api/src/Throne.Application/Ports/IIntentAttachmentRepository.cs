using Throne.Application.Intents;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public interface IIntentAttachmentRepository
{
    Task<int> CountByIntentAsync(IntentId intentId, CancellationToken ct);

    Task<IntentAttachment> AddAsync(
        IntentId intentId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct);

    Task DeleteAllForIntentAsync(IntentId intentId, CancellationToken ct);
}
