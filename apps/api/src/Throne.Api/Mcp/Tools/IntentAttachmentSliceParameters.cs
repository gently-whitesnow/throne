namespace Throne.Api.Mcp.Tools;

internal static class IntentAttachmentSliceParameters
{
    public const int DefaultMaxChars = 50_000;
    public const int AbsoluteMaxChars = 200_000;

    public static int ClampStartOffset(int? offset, int totalBytes)
    {
        var startOffset = offset.GetValueOrDefault(0);
        if (startOffset < 0)
        {
            throw McpToolErrors.ValidationFailed("offset must be non-negative.");
        }
        return startOffset > totalBytes ? totalBytes : startOffset;
    }

    public static int ValidateCharLimit(int? maxChars)
    {
        var charLimit = maxChars.GetValueOrDefault(DefaultMaxChars);
        if (charLimit <= 0)
        {
            throw McpToolErrors.ValidationFailed("max_chars must be positive.");
        }
        if (charLimit > AbsoluteMaxChars)
        {
            throw McpToolErrors.ValidationFailed($"max_chars must be ≤ {AbsoluteMaxChars}.");
        }
        return charLimit;
    }
}
