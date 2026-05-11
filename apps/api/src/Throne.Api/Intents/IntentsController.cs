using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Intents.Events;
using Throne.Application.Intents.Linking;
using Throne.Application.Ports;
using Throne.Application.TextVersions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;
using DomainIntentLinkAuthor = Throne.Domain.Intents.Linking.IntentLinkAuthor;
using DomainIntentLinkDirection = Throne.Application.Ports.IntentLinkDirection;
using FileParameter = Throne.Api.Generated.FileParameter;

namespace Throne.Api.Intents;

public sealed partial class IntentsController(
    ListIntentsHandler listHandler,
    GetIntentHandler getHandler,
    CreateIntentHandler createHandler,
    SetIntentStatusHandler setStatusHandler,
    SetIntentTagsHandler setTagsHandler,
    ReplaceIntentTextHandler replaceHandler,
    DeleteIntentHandler deleteHandler,
    ListIntentVersionsHandler listVersionsHandler,
    UploadIntentAttachmentHandler uploadAttachmentHandler,
    ListIntentAttachmentsHandler listAttachmentsHandler,
    DownloadIntentAttachmentHandler downloadAttachmentHandler,
    DeleteIntentAttachmentHandler deleteAttachmentHandler,
    MoveIntentHandler moveHandler,
    LinkIntentHandler linkHandler,
    UnlinkIntentHandler unlinkHandler,
    ListIntentLinksHandler listLinksHandler,
    GetIntentLinksSummaryHandler linksSummaryHandler,
    ListIntentEventsHandler listEventsHandler,
    IIntentLinkRepository linkRepository,
    ITagRepository tags) : IntentsControllerBase
{
    private const int TextShortMaxLength = 140;

    public override async Task<ActionResult<ICollection<IntentListItemDto>>> ListIntents(IEnumerable<IntentStatus> status = null!)
    {
        var statuses = status?
            .Select(IntentStatusDtoMapper.FromContractStatus)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var intents = await listHandler.HandleAsync(
            new ListIntentsQuery(statuses is { Length: > 0 } ? statuses : null),
            HttpContext.RequestAborted);

        var tagMap = await BuildTagMapAsync(intents.SelectMany(i => i.TagIds), HttpContext.RequestAborted);
        var dtos = new List<IntentListItemDto>(intents.Count);
        foreach (var intent in intents)
        {
            dtos.Add(IntentDtoMapper.ToListDto(intent, tagMap, TextShortMaxLength));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<ICollection<IntentEventDto>>> ListIntentEvents(string id)
    {
        try
        {
            var events = await listEventsHandler.HandleAsync(
                new ListIntentEventsQuery(id), HttpContext.RequestAborted);
            var dtos = new List<IntentEventDto>(events.Count);
            foreach (var e in events)
            {
                dtos.Add(IntentEventDtoMapper.ToEventDto(e));
            }
            return Ok(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> GetIntent(string id)
    {
        try
        {
            var intent = await getHandler.HandleAsync(new GetIntentQuery(id), HttpContext.RequestAborted);
            var links = await linkRepository.ListByIntentAsync(intent.Id, HttpContext.RequestAborted);
            var tagMap = await BuildTagMapAsync(CollectTagIds(intent, links), HttpContext.RequestAborted);
            var linkDtos = links.Select(v => IntentLinkDtoMapper.ToLinkViewDto(v, tagMap)).ToList();
            return Ok(IntentDtoMapper.ToDetailDto(intent, tagMap, linkDtos));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<IntentLinkDto>> CreateIntentLink(string id, CreateIntentLinkRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var link = await linkHandler.HandleAsync(
                new LinkIntentCommand(
                    id,
                    body.To_id,
                    IntentLinkDtoMapper.FromContractLinkType(body.Type),
                    DomainIntentLinkAuthor.User,
                    body.Rationale),
                HttpContext.RequestAborted);
            var location = $"/api/v1/intents/{Uri.EscapeDataString(id)}/links/{Uri.EscapeDataString(link.ToId.Value)}/{Uri.EscapeDataString(link.Type)}";
            return Created(location, IntentLinkDtoMapper.ToLinkDto(link));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapCreateLink(ex);
        }
    }

    public override async Task<IActionResult> DeleteIntentLink(string id, string to_id, ContractIntentLinkType type)
    {
        await unlinkHandler.HandleAsync(
            new UnlinkIntentCommand(id, to_id, IntentLinkDtoMapper.FromContractLinkType(type)),
            HttpContext.RequestAborted);
        return NoContent();
    }

    public override async Task<ActionResult<IntentLinksPageDto>> ListIntentLinks(
        string id,
        ContractIntentLinkDirection? direction = null,
        ContractIntentLinkType? type = null,
        int? limit = null,
        string cursor = null!)
    {
        var page = await listLinksHandler.HandleAsync(
            new ListIntentLinksQuery(
                id,
                MapDirection(direction),
                type is null ? null : IntentLinkDtoMapper.FromContractLinkType(type.Value),
                limit ?? ListIntentLinksHandler.DefaultLimit,
                cursor),
            HttpContext.RequestAborted);

        var tagMap = await BuildTagMapAsync(
            page.Items.SelectMany(v => v.Other.TagIds),
            HttpContext.RequestAborted);

        var dto = new IntentLinksPageDto
        {
            Items = new System.Collections.ObjectModel.Collection<IntentLinkViewDto>(
                [.. page.Items.Select(v => IntentLinkDtoMapper.ToLinkViewDto(v, tagMap))]),
            Next_cursor = page.NextCursor,
        };
        return Ok(dto);
    }

    private static DomainIntentLinkDirection? MapDirection(ContractIntentLinkDirection? direction) => direction switch
    {
        ContractIntentLinkDirection.Outgoing => DomainIntentLinkDirection.Outgoing,
        ContractIntentLinkDirection.Incoming => DomainIntentLinkDirection.Incoming,
        _ => null,
    };

    private static IEnumerable<TagId> CollectTagIds(Intent intent, IReadOnlyList<IntentLinkView> links)
    {
        foreach (var id in intent.TagIds)
        {
            yield return id;
        }
        foreach (var view in links)
        {
            foreach (var id in view.Other.TagIds)
            {
                yield return id;
            }
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> CreateIntent(CreateIntentRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var intent = await createHandler.HandleAsync(
            new CreateIntentCommand(body.Text, body.Tag_names?.ToList(), TextVersionAuthor.User),
            HttpContext.RequestAborted);

        var tagMap = await BuildTagMapAsync(intent.TagIds, HttpContext.RequestAborted);
        var dto = IntentDtoMapper.ToDetailDto(intent, tagMap);
        return CreatedAtAction(nameof(GetIntent), new { id = intent.Id.Value }, dto);
    }

    public override async Task<ActionResult<IntentDetailDto>> SetIntentTags(string id, SetIntentTagsRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await setTagsHandler.HandleAsync(
                new SetIntentTagsCommand(id, body.Expected_version, TagIds: null, body.Tag_names?.ToList()),
                HttpContext.RequestAborted);
            var tagMap = await BuildTagMapAsync(intent.TagIds, HttpContext.RequestAborted);
            return Ok(IntentDtoMapper.ToDetailDto(intent, tagMap));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapSetTags(ex);
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> ReplaceIntentText(string id, ReplaceTextRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await replaceHandler.HandleAsync(
                new ReplaceIntentTextCommand(id, body.Expected_version, body.Old_text, body.New_text, TextVersionAuthor.User),
                HttpContext.RequestAborted);
            var tagMap = await BuildTagMapAsync(intent.TagIds, HttpContext.RequestAborted);
            return Ok(IntentDtoMapper.ToDetailDto(intent, tagMap));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapReplace(ex);
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> SetIntentStatus(string id, SetIntentStatusRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await setStatusHandler.HandleAsync(
                new SetIntentStatusCommand(
                    id,
                    IntentStatusDtoMapper.FromContractStatus(body.Status),
                    body.Reject_reason,
                    IntentTrainingAuthor.User,
                    "http:set_intent_status"),
                HttpContext.RequestAborted);

            var tagMap = await BuildTagMapAsync(intent.TagIds, HttpContext.RequestAborted);
            return Ok(IntentDtoMapper.ToDetailDto(intent, tagMap));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapSetStatus(ex);
        }
    }

    public override async Task<ActionResult<IntentDetailDto>> MoveIntent(string id, MoveIntentRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await moveHandler.HandleAsync(
                new MoveIntentCommand(id, body.Before_id, body.After_id),
                HttpContext.RequestAborted);
            var tagMap = await BuildTagMapAsync(intent.TagIds, HttpContext.RequestAborted);
            return Ok(IntentDtoMapper.ToDetailDto(intent, tagMap));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapMove(ex);
        }
    }

    public override async Task<IActionResult> DeleteIntent(string id)
    {
        try
        {
            await deleteHandler.HandleAsync(new DeleteIntentCommand(id), HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<ICollection<TextVersionDto>>> ListIntentVersions(string id)
    {
        try
        {
            var versions = await listVersionsHandler.HandleAsync(
                new ListIntentVersionsQuery(id), HttpContext.RequestAborted);
            var dtos = new List<TextVersionDto>(versions.Count);
            foreach (var v in versions)
            {
                dtos.Add(TextVersionDtoMapper.ToDto(v));
            }
            return Ok(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<ICollection<IntentAttachmentDto>>> ListIntentAttachments(string id)
    {
        try
        {
            var attachments = await listAttachmentsHandler.HandleAsync(
                new ListIntentAttachmentsQuery(id), HttpContext.RequestAborted);
            var dtos = new List<IntentAttachmentDto>(attachments.Count);
            foreach (var attachment in attachments)
            {
                dtos.Add(IntentDtoMapper.ToAttachmentDto(attachment));
            }
            return Ok(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }

    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public override async Task<ActionResult<IntentAttachmentDto>> UploadIntentAttachment(string id, FileParameter file = default!)
    {
        _ = file;
        if (!Request.HasFormContentType)
        {
            return UnprocessableEntity(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ValidationFailed,
                    "Request must be multipart/form-data.",
                    new Dictionary<string, object?> { ["content_type"] = Request.ContentType ?? string.Empty })));
        }

        var formFile = Request.Form.Files.GetFile("file");
        if (formFile is null || formFile.Length < 1)
        {
            return UnprocessableEntity(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ValidationFailed,
                    "Multipart field \"file\" is required and must be non-empty.",
                    new Dictionary<string, object?> { ["field"] = "file" })));
        }

        try
        {
            await using var stream = formFile.OpenReadStream();
            var attachment = await uploadAttachmentHandler.HandleAsync(
                new UploadIntentAttachmentCommand(
                    id,
                    stream,
                    formFile.FileName,
                    formFile.ContentType ?? "application/octet-stream",
                    formFile.Length),
                HttpContext.RequestAborted);

            var location = $"/api/v1/intents/{Uri.EscapeDataString(id)}/attachments/{Uri.EscapeDataString(attachment.Id)}";
            return Created(location, IntentDtoMapper.ToAttachmentDto(attachment));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapUploadAttachment(ex);
        }
    }

    public override async Task<IActionResult> DownloadIntentAttachment(string id, string attachment_id)
    {
        try
        {
            var attachment = await downloadAttachmentHandler.HandleAsync(
                new DownloadIntentAttachmentQuery(id, attachment_id), HttpContext.RequestAborted);
            return File(attachment.Content, attachment.Attachment.ContentType, attachment.Attachment.FileName);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentAttachmentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Attachment not found", ex.Detail));
        }
    }

    public override async Task<IActionResult> DeleteIntentAttachment(string id, string attachment_id)
    {
        try
        {
            await deleteAttachmentHandler.HandleAsync(
                new DeleteIntentAttachmentCommand(id, attachment_id), HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentAttachmentNotFound)
        {
            return NotFound(ApiProblems.NotFound("Attachment not found", ex.Detail));
        }
    }

    private async Task<IReadOnlyDictionary<string, Tag>> BuildTagMapAsync(
        IEnumerable<TagId> tagIds,
        CancellationToken ct)
    {
        var ids = tagIds.Select(t => t.Value).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, Tag>(StringComparer.Ordinal);
        }

        var all = await tags.ListAllAsync(ct);
        var map = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var t in all)
        {
            if (ids.Contains(t.Id.Value))
            {
                map[t.Id.Value] = t;
            }
        }
        return map;
    }
}
