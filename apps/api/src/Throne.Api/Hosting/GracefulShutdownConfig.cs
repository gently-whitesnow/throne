using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Throne.Api.Hosting;

/// <summary>
/// Bounds the graceful-shutdown backstop. Long-lived connections and background loops
/// are expected to observe <c>ApplicationStopping</c> and unwind promptly themselves
/// (see <see cref="Mcp.McpKeepAliveMiddleware"/>, <see cref="Realtime.RealtimeController"/>
/// and the infrastructure <c>BackgroundService</c>s) — this only caps the worst case for
/// anything that fails to. The framework default of 30s is needlessly long for that role.
///
/// Extracted from <c>Program.cs</c> so <see cref="HostOptions"/> does not count against
/// the <c>Main</c> class-coupling budget (CA1506), mirroring
/// <see cref="Auth.ForwardedHeadersConfig"/>.
/// </summary>
public static class GracefulShutdownConfig
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddBoundedGracefulShutdown(this IServiceCollection services)
    {
        services.Configure<HostOptions>(o => o.ShutdownTimeout = ShutdownTimeout);
        return services;
    }
}
