using System.Net;

namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal sealed class GenericHttpApiException(HttpStatusCode statusCode, string? body)
    : Exception($"Generic task-tracker API request failed: HTTP {(int)statusCode} ({statusCode}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Body { get; } = body;
}
