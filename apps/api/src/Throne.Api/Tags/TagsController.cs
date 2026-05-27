using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Tags;
using Throne.Domain.Tags;
using Throne.Tags.Contracts.Generated;

namespace Throne.Api.Tags;

public sealed class TagsController(
    ListTagsHandler listHandler,
    CreateTagHandler createHandler,
    RenameTagHandler renameHandler,
    DeleteTagHandler deleteHandler,
    GetTagUsageHandler usageHandler,
    GetTagHandler getHandler,
    SetTagDefaultRepositoriesHandler setDefaultsHandler) : TagsControllerBase
{
    public override async Task<ActionResult<ICollection<TagDto>>> ListTags()
    {
        var tags = await listHandler.HandleAsync(new ListTagsQuery(), HttpContext.RequestAborted);
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
            var tag = await createHandler.HandleAsync(new CreateTagCommand(body.Name), HttpContext.RequestAborted);
            var dto = TagDtoMapper.ToDto(tag);
            return CreatedAtAction(nameof(GetTagUsage), new { id = tag.Id.Value }, dto);
        }
        catch (ApiException ex)
        {
            return TagsErrorMapper.Map<TagDto>(ex);
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
            return TagsErrorMapper.Map<TagDto>(ex);
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
            return TagsErrorMapper.Map<DeleteTagResponse>(ex);
        }
    }

    public override async Task<ActionResult<TagUsageDto>> GetTagUsage(string id)
    {
        try
        {
            var usage = await usageHandler.HandleAsync(new GetTagUsageQuery(id), HttpContext.RequestAborted);
            return Ok(new TagUsageDto
            {
                Tag_id = id,
                Intents_count = usage.IntentsCount,
            });
        }
        catch (ApiException ex)
        {
            return TagsErrorMapper.Map<TagUsageDto>(ex);
        }
    }

    public override async Task<ActionResult<TagDetailDto>> GetTag(string id)
    {
        try
        {
            var tag = await getHandler.HandleAsync(new GetTagQuery(id), HttpContext.RequestAborted);
            return Ok(TagDtoMapper.ToDetailDto(tag));
        }
        catch (ApiException ex)
        {
            return TagsErrorMapper.Map<TagDetailDto>(ex);
        }
    }

    public override async Task<ActionResult<TagDetailDto>> SetTagDefaultRepositories(
        string id,
        SetTagDefaultRepositoriesRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        IReadOnlyList<Domain.Tags.TagDefaultRepository> domain;
        try
        {
            domain = body.Default_repositories.Select(TagDtoMapper.FromDefaultRepoDto).ToArray();
        }
        catch (ArgumentException ex)
        {
            return new UnprocessableEntityObjectResult(Throne.Api.Shared.ApiProblems.Build(
                Microsoft.AspNetCore.Http.StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(ErrorCodes.ValidationFailed, ex.Message)));
        }

        try
        {
            var tag = await setDefaultsHandler.HandleAsync(
                new SetTagDefaultRepositoriesCommand(id, body.Expected_version, domain),
                HttpContext.RequestAborted);
            return Ok(TagDtoMapper.ToDetailDto(tag));
        }
        catch (ApiException ex)
        {
            return TagsErrorMapper.Map<TagDetailDto>(ex);
        }
    }
}
