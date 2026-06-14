using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.PromptParts;
using Throne.Application.TextVersions;
using Throne.PromptParts.Contracts.Generated;

namespace Throne.Api.PromptParts;

public sealed class PromptPartsController(
    ListPromptPartsHandler listHandler,
    GetPromptPartHandler getHandler,
    CreatePromptPartHandler createHandler,
    ReplacePromptPartTextHandler replaceHandler,
    SetPromptPartRolesHandler setRolesHandler,
    DeletePromptPartHandler deleteHandler,
    GetBundlesTreeHandler bundlesTreeHandler,
    ListPromptPartVersionsHandler listVersionsHandler
) : PromptPartsControllerBase
{
    public override async Task<ActionResult<BundlesTreeDto>> GetBundlesTree()
    {
        var tree = await bundlesTreeHandler.HandleAsync(
            new GetBundlesTreeQuery(),
            HttpContext.RequestAborted
        );
        return Ok(BundlesTreeDtoMapper.ToDto(tree));
    }

    public override async Task<ActionResult<ICollection<TextVersionDto>>> ListPromptPartVersions(
        string id
    )
    {
        var versions = await listVersionsHandler.HandleAsync(
            new ListPromptPartVersionsQuery(id),
            HttpContext.RequestAborted
        );
        var dtos = new List<TextVersionDto>(versions.Count);
        foreach (var v in versions)
        {
            dtos.Add(TextVersionDtoMapper.ToDto(v));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<ICollection<PromptPartListItemDto>>> ListPromptParts()
    {
        var parts = await listHandler.HandleAsync(
            new ListPromptPartsQuery(),
            HttpContext.RequestAborted
        );
        var dtos = new List<PromptPartListItemDto>(parts.Count);
        foreach (var part in parts)
        {
            dtos.Add(PromptPartDtoMapper.ToListDto(part));
        }
        return Ok(dtos);
    }

    public override async Task<ActionResult<PromptPartDto>> CreatePromptPart(
        CreatePromptPartRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var part = await createHandler.HandleAsync(
            new CreatePromptPartCommand(
                body.Key,
                body.Text,
                body.Description,
                PromptPartDtoMapper.ToDomainRoles(body.Mode_roles)
            ),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartDtoMapper.ToDetailDto(part));
    }

    public override async Task<ActionResult<PromptPartDto>> GetPromptPart(string id)
    {
        var part = await getHandler.HandleAsync(
            new GetPromptPartQuery(id),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartDtoMapper.ToDetailDto(part));
    }

    public override async Task<ActionResult<PromptPartDto>> ReplacePromptPartText(
        string id,
        ReplaceTextRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var part = await replaceHandler.HandleAsync(
            new ReplacePromptPartTextCommand(
                id,
                body.Expected_version,
                body.Old_text,
                body.New_text
            ),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartDtoMapper.ToDetailDto(part));
    }

    public override async Task<ActionResult<PromptPartDto>> SetPromptPartRoles(
        string id,
        SetPromptPartRolesRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var part = await setRolesHandler.HandleAsync(
            new SetPromptPartRolesCommand(id, PromptPartDtoMapper.ToDomainRoles(body.Mode_roles)),
            HttpContext.RequestAborted
        );
        return Ok(PromptPartDtoMapper.ToDetailDto(part));
    }

    public override async Task<ActionResult<DeletePromptPartResponse>> DeletePromptPart(string id)
    {
        await deleteHandler.HandleAsync(
            new DeletePromptPartCommand(id),
            HttpContext.RequestAborted
        );
        return Ok(new DeletePromptPartResponse { Prompt_part_id = id });
    }
}
