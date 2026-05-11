using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;

namespace Throne.Api.Shared;

internal static class ApiProblems
{
    public static ProblemDetails NotFound(string title, string detail) => new()
    {
        Type = "about:blank",
        Title = title,
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
    };

    public static ProblemDetails Build(int status, string title, string detail) => new()
    {
        Type = "about:blank",
        Title = title,
        Status = status,
        Detail = detail,
    };

    public static ProblemDetails Build(int status, string title, ApiException ex)
    {
        var problem = new ProblemDetails
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
