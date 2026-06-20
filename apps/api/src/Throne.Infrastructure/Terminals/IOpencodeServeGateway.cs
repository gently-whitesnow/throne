namespace Throne.Infrastructure.Terminals;

internal interface IOpencodeServeGateway
{
    /// <summary>
    /// Idempotently ensures the shared persistent <c>opencode serve</c> is running and healthy,
    /// returning its base <see cref="Uri"/>. The serve lives in a fixed-name tmux session
    /// (<c>throne-opencode-serve</c>) so it survives a Throne restart and a slow/restarted attach
    /// front. Safe to call repeatedly and concurrently — a healthy serve is a no-op fast path.
    /// </summary>
    Task<Uri> EnsureRunningAsync(CancellationToken ct);
}
