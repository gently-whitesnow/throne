using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Application.ChatUploads;

public sealed class DeleteChatUploadHandler(
    IChatUploadRepository uploads,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var outcome = await unitOfWork.ExecuteOutsideTransactionAsync(
            inner => uploads.DeleteAsync(id, inner),
            ct);

        if (outcome is DeleteChatUploadOutcome.NotFound)
        {
            throw new ApiException(
                ErrorCodes.ChatUploadNotFound,
                $"Chat upload '{id}' not found.",
                new Dictionary<string, object?> { ["chat_upload_id"] = id });
        }
    }
}
