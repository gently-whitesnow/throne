using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;
using ContractTrainingAuthor = Throne.Intents.Contracts.Generated.IntentTrainingAuthor;
using DomainTrainingAuthor = Throne.Domain.Intents.Training.IntentTrainingAuthor;

namespace Throne.Api.Intents;

internal static class IntentDtoMapper
{
    public static IntentDetailDto ToDetailDto(Intent intent, IReadOnlyDictionary<string, Tag> tagsById) => new()
    {
        Id = intent.Id.Value,
        Status = ToContractStatus(intent.Status),
        Current_version = intent.CurrentVersion,
        Tags = ToTagRefs(intent.TagIds, tagsById),
        Text = intent.Text,
        Created_at = intent.CreatedAt,
        Updated_at = intent.UpdatedAt,
    };

    public static IntentListItemDto ToListDto(Intent intent, IReadOnlyDictionary<string, Tag> tagsById, int textShortMaxLength) => new()
    {
        Id = intent.Id.Value,
        Status = ToContractStatus(intent.Status),
        Current_version = intent.CurrentVersion,
        Tags = ToTagRefs(intent.TagIds, tagsById),
        Text_short = TextShort(intent.Text, textShortMaxLength),
        Created_at = intent.CreatedAt,
        Updated_at = intent.UpdatedAt,
    };

    public static IntentAttachmentDto ToAttachmentDto(IntentAttachment attachment) => new()
    {
        Id = attachment.Id,
        Intent_id = attachment.IntentId,
        File_name = attachment.FileName,
        Content_type = attachment.ContentType,
        Size_bytes = attachment.SizeBytes,
        Created_at = attachment.CreatedAt,
    };

    public static IntentQaDto ToQaDto(IntentQa qa) => new()
    {
        Id = qa.Id,
        Intent_id = qa.IntentId.Value,
        Intent_version_at_write = qa.IntentVersionAtWrite,
        Question = qa.Question,
        Answer = qa.Answer,
        Created_at = qa.CreatedAt,
        Created_by = ToContractTrainingAuthor(qa.CreatedBy),
    };

    public static IntentReviewDto ToReviewDto(IntentReview r) => new()
    {
        Id = r.Id,
        Intent_id = r.IntentId.Value,
        Intent_version_at_write = r.IntentVersionAtWrite,
        Note = r.Note,
        Reason = r.Reason,
        Created_at = r.CreatedAt,
        Created_by = ToContractTrainingAuthor(r.CreatedBy),
    };

    public static IntentStatus ToContractStatus(string status) => status switch
    {
        IntentStatusNames.Draft => IntentStatus.Draft,
        IntentStatusNames.Interview => IntentStatus.Interview,
        IntentStatusNames.ReadyForWork => IntentStatus.Ready_for_work,
        IntentStatusNames.Work => IntentStatus.Work,
        IntentStatusNames.ReadyForReview => IntentStatus.Ready_for_review,
        IntentStatusNames.Done => IntentStatus.Done,
        IntentStatusNames.Reject => IntentStatus.Reject,
        _ => throw new InvalidOperationException($"Unknown domain status: {status}"),
    };

    public static ContractTrainingAuthor ToContractTrainingAuthor(DomainTrainingAuthor author) => author switch
    {
        DomainTrainingAuthor.User => ContractTrainingAuthor.User,
        DomainTrainingAuthor.Agent => ContractTrainingAuthor.Agent,
        DomainTrainingAuthor.System => ContractTrainingAuthor.System,
        _ => throw new InvalidOperationException($"Unknown training author: {author}"),
    };

    private static List<TagRefDto> ToTagRefs(IReadOnlyList<TagId> tagIds, IReadOnlyDictionary<string, Tag> tagsById)
    {
        var refs = new List<TagRefDto>(tagIds.Count);
        foreach (var id in tagIds)
        {
            if (!tagsById.TryGetValue(id.Value, out var tag))
            {
                continue;
            }

            refs.Add(new TagRefDto { Id = tag.Id.Value, Name = tag.Name });
        }
        return refs;
    }

    private static string TextShort(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
