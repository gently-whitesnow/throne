using System.Net.Http.Headers;
using System.Text;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Applies OpenCode server Basic auth to outbound HTTP requests, mirroring how the
/// <c>opencode serve</c> / <c>opencode attach</c> CLIs read the same environment variables
/// (<c>OPENCODE_SERVER_PASSWORD</c>, optional <c>OPENCODE_SERVER_USERNAME</c>, default
/// <c>opencode</c>). No password set ⇒ no header, so an unauthenticated loopback serve keeps
/// working. Shared by the serve gateway (health checks) and the TUI client (session API).
/// </summary>
internal static class OpencodeServerAuth
{
    public static void Apply(HttpRequestMessage request)
    {
        var password = Environment.GetEnvironmentVariable("OPENCODE_SERVER_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        var username = Environment.GetEnvironmentVariable("OPENCODE_SERVER_USERNAME");
        if (string.IsNullOrEmpty(username))
        {
            username = "opencode";
        }

        var raw = Encoding.UTF8.GetBytes(username + ":" + password);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
    }
}
