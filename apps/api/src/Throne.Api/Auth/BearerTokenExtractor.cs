using Microsoft.AspNetCore.Http;

namespace Throne.Api.Auth;

internal static class BearerTokenExtractor
{
    public static string? Extract(HttpRequest request) =>
        BearerHeaderToken.Read(request) ?? BearerQueryToken.Read(request);
}

internal static class BearerHeaderToken
{
    private const string BearerPrefix = "Bearer ";

    public static string? Read(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var auth) || auth.Count == 0)
        {
            return null;
        }
        var raw = auth[0];
        if (string.IsNullOrEmpty(raw) || !raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var value = raw[BearerPrefix.Length..].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

internal static class BearerQueryToken
{
    public static string? Read(HttpRequest request)
    {
        if (!request.Query.TryGetValue("token", out var qs) || qs.Count == 0)
        {
            return null;
        }
        var value = qs[0];
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
