using Throne.Application.Tags;

namespace Throne.Api.Tags;

// Opaque base64 keyset cursor `usageCount|id` for the fixed usage-desc tag order.
// Same convention as IntentListCursorCodec.
internal static class TagListCursorCodec
{
    public static string? Encode(TagListCursor? cursor)
    {
        if (cursor is null)
        {
            return null;
        }
        var raw = $"{cursor.UsageCount}|{cursor.Id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    public static TagListCursor? Parse(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("cursor is not a valid base64 token.", nameof(cursor), ex);
        }

        var parts = decoded.Split('|');
        if (parts.Length != 2
            || !int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var usageCount)
            || string.IsNullOrEmpty(parts[1]))
        {
            throw new ArgumentException("cursor payload is malformed.", nameof(cursor));
        }

        return new TagListCursor(usageCount, parts[1]);
    }
}
