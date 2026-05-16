namespace Throne.Api.Mcp.Tools;

internal static class Utf8WindowAligner
{
    /// <summary>
    /// Counts leading UTF-8 continuation bytes (10xxxxxx) so the caller can skip
    /// them and start decoding at the next code-point boundary. Returns 0 when
    /// startOffset == 0 — by definition the slice begins on a rune boundary.
    /// </summary>
    public static int CountLeadingContinuationBytes(ReadOnlySpan<byte> window, int startOffset)
    {
        if (startOffset == 0)
        {
            return 0;
        }

        var skip = 0;
        while (skip < window.Length && (window[skip] & 0xC0) == 0x80)
        {
            skip++;
        }
        return skip;
    }
}
