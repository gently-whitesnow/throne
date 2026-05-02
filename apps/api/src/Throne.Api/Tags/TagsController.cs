using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Tags;
using Throne.Tags.Contracts.Generated;

namespace Throne.Api.Tags;

public sealed class TagsController(
    ListTagsHandler listHandler,
    CreateTagHandler createHandler,
    RenameTagHandler renameHandler,
    DeleteTagHandler deleteHandler,
    GetTagUsageHandler usageHandler) : TagsControllerBase
{
    public override async Task<ActionResult<ICollection<TagDto>>> ListTags()
    {
        var tags = await listHandler.HandleAsync(new ListTagsQuery(), HttpContext.RequestAborted)
            ;

        var dtos = new List<TagDto>(tags.Count);
        foreach (var tag in tags)
        {
            dtos.Add(TagDtoMapper.ToDto(tag));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<TagDto>> CreateTag(CreateTagRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var tag = await createHandler.HandleAsync(new CreateTagCommand(body.Name), HttpContext.RequestAborted)
                ;
            var dto = TagDtoMapper.ToDto(tag);
            return CreatedAtAction(nameof(GetTagUsage), new { id = tag.Id.Value }, dto);
        }
        catch (ApiException ex)
        {
            return MapTagError(ex);
        }
    }

    public override async Task<ActionResult<TagDto>> RenameTag(string id, RenameTagRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var tag = await renameHandler.HandleAsync(
                new RenameTagCommand(id, body.Expected_version, body.Name),
                HttpContext.RequestAborted);
            return Ok(TagDtoMapper.ToDto(tag));
        }
        catch (ApiException ex)
        {
            return MapTagError(ex);
        }
    }

    public override async Task<ActionResult<DeleteTagResponse>> DeleteTag(string id, bool? detach = false)
    {
        try
        {
            var result = await deleteHandler.HandleAsync(
                new DeleteTagCommand(id, ConfirmDetach: detach ?? false),
                HttpContext.RequestAborted);
            return Ok(new DeleteTagResponse
            {
                Tag_id = id,
                Intents_detached = result.IntentsDetached,
            });
        }
        catch (ApiException ex)
        {
            return MapTagError<DeleteTagResponse>(ex);
        }
    }

    public override async Task<ActionResult<TagUsageDto>> GetTagUsage(string id)
    {
        try
        {
            var usage = await usageHandler.HandleAsync(new GetTagUsageQuery(id), HttpContext.RequestAborted)
                ;
            return Ok(new TagUsageDto
            {
                Tag_id = id,
                Intents_count = usage.IntentsCount,
            });
        }
        catch (ApiException ex)
        {
            return MapTagError<TagUsageDto>(ex);
        }
    }

    private ActionResult<TagDto> MapTagError(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.TagNotFound => NotFound(NotFoundProblem("Tag not found", ex.Detail)),
            ErrorCodes.TagNameTaken => Conflict(BuildProblem(StatusCodes.Status409Conflict, "Tag name already taken", ex)),
            ErrorCodes.TagVersionConflict => Conflict(BuildProblem(StatusCodes.Status409Conflict, "Tag version conflict", ex)),
            ErrorCodes.TagInUse => Conflict(BuildProblem(StatusCodes.Status409Conflict, "Tag in use", ex)),
            ErrorCodes.TagNameInvalid => UnprocessableEntity(BuildProblem(StatusCodes.Status422UnprocessableEntity, "Invalid tag name", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    private ActionResult<T> MapTagError<T>(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.TagNotFound => NotFound(NotFoundProblem("Tag not found", ex.Detail)),
            ErrorCodes.TagNameTaken => Conflict(BuildProblem(StatusCodes.Status409Conflict, "Tag name already taken", ex)),
            ErrorCodes.TagVersionConflict => Conflict(BuildProblem(StatusCodes.Status409Conflict, "Tag version conflict", ex)),
            ErrorCodes.TagInUse => Conflict(BuildProblem(StatusCodes.Status409Conflict, "Tag in use", ex)),
            ErrorCodes.TagNameInvalid => UnprocessableEntity(BuildProblem(StatusCodes.Status422UnprocessableEntity, "Invalid tag name", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails NotFoundProblem(string title, string detail) => new()
    {
        Type = "about:blank",
        Title = title,
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
    };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails BuildProblem(int status, string title, ApiException ex)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Detail = ex.Detail,
        };
        problem.Extensions["code"] = ex.Code;
        foreach (var (key, value) in ex.Extensions)
        {
            problem.Extensions[key] = value;
        }
        return problem;
    }
}
