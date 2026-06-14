using Microsoft.AspNetCore.Diagnostics;
using Throne.Application.Errors;

namespace Throne.Api.Shared;

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ApiException apiException)
        {
            return false;
        }

        var status = ApiErrorRegistry.GetStatus(apiException.Code);
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            ApiProblems.Build(status, "API error", apiException),
            cancellationToken);

        return true;
    }
}
