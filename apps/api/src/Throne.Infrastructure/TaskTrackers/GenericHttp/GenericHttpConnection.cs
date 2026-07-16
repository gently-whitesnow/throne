namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal sealed record GenericHttpConnection(string BaseUrl, string Token)
{
    public string ApiBaseUrl => $"{BaseUrl.TrimEnd('/')}/api/task-tracker";
}
