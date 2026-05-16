using System.Text;

namespace Throne.Api.Mcp.Tools;

internal static class IntentAttachmentTextSlicer
{
    public static IntentAttachmentTextSlice Slice(
        string contentType,
        byte[] source,
        int? offset,
        int? maxChars)
    {
        var totalBytes = source.Length;
        var startOffset = IntentAttachmentSliceParameters.ClampStartOffset(offset, totalBytes);
        var charLimit = IntentAttachmentSliceParameters.ValidateCharLimit(maxChars);

        var maxWindowBytes = checked((long)charLimit * 4);
        var windowEndExclusive = (int)Math.Min(totalBytes, startOffset + maxWindowBytes);
        var window = source.AsSpan(startOffset, windowEndExclusive - startOffset);

        var skipLeading = Utf8WindowAligner.CountLeadingContinuationBytes(window, startOffset);
        var decoded = DecodeAndCap(window[skipLeading..], charLimit, out var truncatedByCharLimit);

        var returnedByteLength = Encoding.UTF8.GetByteCount(decoded);
        var returnedBytesStart = startOffset + skipLeading;
        var returnedBytesEnd = returnedBytesStart + returnedByteLength;
        var truncated = truncatedByCharLimit || returnedBytesEnd < totalBytes;

        return new IntentAttachmentTextSlice(
            contentType,
            totalBytes,
            returnedBytesStart,
            returnedBytesEnd,
            truncated,
            decoded);
    }

    private static string DecodeAndCap(ReadOnlySpan<byte> trimmed, int charLimit, out bool truncatedByCharLimit)
    {
        var decoded = Encoding.UTF8.GetString(trimmed);
        truncatedByCharLimit = decoded.Length > charLimit;
        return truncatedByCharLimit ? decoded[..charLimit] : decoded;
    }
}
