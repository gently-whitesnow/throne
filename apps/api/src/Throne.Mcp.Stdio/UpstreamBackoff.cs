namespace Throne.Mcp.Stdio;

internal static class UpstreamBackoff
{
    public static TimeSpan Compute(int attempt)
    {
        var capped = Math.Min(attempt, 4); // 1s, 2s, 4s, 8s, then plateau with jitter.
        var baseMs = 1000 * (1 << (capped - 1));
        var jitter = Random.Shared.Next(0, 250);
        return TimeSpan.FromMilliseconds(Math.Min(baseMs + jitter, 10_000));
    }
}
