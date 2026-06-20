using System.Net;
using System.Net.Sockets;

namespace Throne.Infrastructure.Terminals;

internal sealed class OpencodeTuiPortAllocator : IOpencodeTuiPortAllocator
{
    public int Allocate()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
