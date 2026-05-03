using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.DreamRuns;
using Throne.Application.Errors;
using Throne.Dream.Contracts.Generated;

namespace Throne.Api.Dream;

public sealed class DreamController(
    GetDreamReadinessHandler readinessHandler,
    ListPendingDreamRunsHandler listPendingHandler,
    GetPendingProposalsCountHandler pendingCountHandler,
    GetDreamRunHandler getRunHandler,
    ApplyDreamProposalHandler applyHandler,
    SkipDreamProposalHandler skipHandler,
    CloseDreamRunHandler closeHandler) : DreamControllerBase
{
    public override async Task<ActionResult<DreamReadinessDto>> GetDreamReadiness()
    {
        var snapshot = await readinessHandler.HandleAsync(new GetDreamReadinessQuery(), HttpContext.RequestAborted);
        return Ok(DreamDtoMapper.ToReadinessDto(snapshot));
    }

    public override async Task<ActionResult<ICollection<DreamRunDto>>> ListPendingDreamRuns()
    {
        var runs = await listPendingHandler.HandleAsync(new ListPendingDreamRunsQuery(), HttpContext.RequestAborted);
        return Ok(runs.Select(DreamDtoMapper.ToRunDto).ToList());
    }

    public override async Task<ActionResult<DreamPendingCountDto>> GetPendingDreamProposalsCount()
    {
        var count = await pendingCountHandler.HandleAsync(new GetPendingProposalsCountQuery(), HttpContext.RequestAborted);
        return Ok(new DreamPendingCountDto { Pending_proposals_count = count });
    }

    public override async Task<ActionResult<DreamRunDetailDto>> GetDreamRun(string run_id)
    {
        try
        {
            var result = await getRunHandler.HandleAsync(new GetDreamRunQuery(run_id), HttpContext.RequestAborted);
            return Ok(DreamDtoMapper.ToDetailDto(result));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.DreamRunNotFound)
        {
            return NotFound(NotFoundProblem("DreamRun not found", ex.Detail));
        }
    }

    public override async Task<ActionResult<DreamRunDto>> CloseDreamRun(string run_id, CloseDreamRunRequest body = null!)
    {
        try
        {
            var run = await closeHandler.HandleAsync(
                new CloseDreamRunCommand(run_id, body?.Release_evidence),
                HttpContext.RequestAborted);
            return Ok(DreamDtoMapper.ToRunDto(run));
        }
        catch (ApiException ex)
        {
            return ex.Code switch
            {
                ErrorCodes.DreamRunNotFound => NotFound(NotFoundProblem("DreamRun not found", ex.Detail)),
                ErrorCodes.DreamRunAlreadyClosed => Conflict(BuildProblem(
                    StatusCodes.Status409Conflict, "DreamRun already closed", ex)),
                _ => throw new InvalidOperationException($"Unexpected API error: {ex.Code}", ex),
            };
        }
    }

    public override async Task<ActionResult<DreamRunDto>> ApplyDreamProposal(
        string run_id, string proposal_id, ApplyDreamProposalRequest body = null!)
    {
        try
        {
            var run = await applyHandler.HandleAsync(
                new ApplyDreamProposalCommand(run_id, proposal_id, body?.Final_rule),
                HttpContext.RequestAborted);
            return Ok(DreamDtoMapper.ToRunDto(run));
        }
        catch (ApiException ex)
        {
            return MapDecisionError(ex);
        }
    }

    public override async Task<ActionResult<DreamRunDto>> SkipDreamProposal(
        string run_id, string proposal_id, SkipDreamProposalRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var run = await skipHandler.HandleAsync(
                new SkipDreamProposalCommand(run_id, proposal_id, body.Reason),
                HttpContext.RequestAborted);
            return Ok(DreamDtoMapper.ToRunDto(run));
        }
        catch (ApiException ex)
        {
            return MapDecisionError(ex);
        }
    }

    private ActionResult<DreamRunDto> MapDecisionError(ApiException ex) => ex.Code switch
    {
        ErrorCodes.DreamRunNotFound => NotFound(NotFoundProblem("DreamRun not found", ex.Detail)),
        ErrorCodes.DreamProposalNotFound => NotFound(NotFoundProblem("Proposal not found", ex.Detail)),
        ErrorCodes.InstructionNotFound => NotFound(NotFoundProblem("Instruction not found", ex.Detail)),
        ErrorCodes.DreamRunAlreadyClosed => Conflict(BuildProblem(
            StatusCodes.Status409Conflict, "DreamRun already closed", ex)),
        ErrorCodes.DreamProposalAlreadyDecided => Conflict(BuildProblem(
            StatusCodes.Status409Conflict, "Proposal already decided", ex)),
        ErrorCodes.DreamProposalNeedsRebase => Conflict(BuildProblem(
            StatusCodes.Status409Conflict, "Proposal needs rebase", ex)),
        ErrorCodes.ValidationFailed => UnprocessableEntity(BuildProblem(
            StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error: {ex.Code}", ex),
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
