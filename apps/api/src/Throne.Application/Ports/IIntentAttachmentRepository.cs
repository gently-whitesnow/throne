using Throne.Application.Intents;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public interface IIntentAttachmentRepository
{
    Task<int> CountByIntentAsync(IntentId intentId, CancellationToken ct);

    Task<IReadOnlyList<IntentAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct);

    Task<UploadIntentAttachmentOutcome> AddAsync(
        IntentId intentId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct);

    Task<IntentAttachmentContent?> OpenContentAsync(IntentId intentId, string attachmentId, CancellationToken ct);

    Task<DeleteIntentAttachmentOutcome> DeleteAsync(IntentId intentId, string attachmentId, CancellationToken ct);

    Task DeleteAllForIntentAsync(IntentId intentId, CancellationToken ct);
}
