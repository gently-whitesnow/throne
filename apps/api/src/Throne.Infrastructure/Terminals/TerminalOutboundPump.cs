using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Tails the per-bridge FIFO (filled by <c>tmux pipe-pane</c>) and forwards every
/// chunk to the WebSocket as an <c>output</c> frame. Exits when the FIFO is closed
/// permanently or cancellation fires.
/// </summary>
internal static class TerminalOutboundPump
{
    private const int OutputChunkBytes = 4096;

    public static async Task RunAsync(ITerminalSocket connection, string fifoPath, CancellationToken ct)
    {
        await using var reader = OpenFifo(fifoPath);
        var buffer = new byte[OutputChunkBytes];
        while (!ct.IsCancellationRequested)
        {
            var read = await TryReadAsync(reader, buffer, ct);
            if (read < 0)
            {
                return;
            }
            if (read == 0)
            {
                await Task.Delay(50, ct);
                continue;
            }
            var text = Encoding.UTF8.GetString(buffer, 0, read);
            await connection.SendTextAsync(TerminalFrames.EncodeOutput(text), ct);
        }
    }

    private static FileStream OpenFifo(string fifoPath) => new(
        fifoPath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite,
        bufferSize: OutputChunkBytes,
        useAsync: true);

    private static async Task<int> TryReadAsync(FileStream reader, byte[] buffer, CancellationToken ct)
    {
        try
        {
            return await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
    }
}
