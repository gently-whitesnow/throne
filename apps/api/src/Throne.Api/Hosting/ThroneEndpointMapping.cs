using Throne.Api.Runtime;
using Throne.Api.Terminals;

namespace Throne.Api.Hosting;

public static class ThroneEndpointMapping
{
    /// <summary>
    /// Composite endpoint mapping for custom Throne surfaces beyond MapControllers().
    /// Today: the embedded-terminal WebSocket bridge. Also enables the WebSocket
    /// middleware required by the bridge.
    /// </summary>
    public static WebApplication MapThroneEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseWebSockets();
        app.MapThroneTerminalEndpoints();
        app.MapThroneRuntimeEndpoints();
        return app;
    }
}
