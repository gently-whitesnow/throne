using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class ThroneMcpServerConfig
{
    public const string Name = "throne";

    public static string Url(string? apiBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(apiBaseUrl)
            ? SessionHookOptions.DefaultApiBaseUrl
            : apiBaseUrl.TrimEnd('/');
        return normalized + "/mcp";
    }
}
