using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
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

    Task<IReadOnlyList<PendingCompressionItem>> ListPendingCompressionAsync(int batchSize, CancellationToken ct);

    Task<Stream?> OpenRawContentAsync(string gridFsId, CancellationToken ct);

    Task ApplyCompressionAsync(
        string attachmentId,
        string previousGridFsId,
        DownscaledImage compressed,
        CancellationToken ct);
}
