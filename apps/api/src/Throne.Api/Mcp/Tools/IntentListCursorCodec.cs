using Throne.Application.Intents;

namespace Throne.Api.Mcp.Tools;

internal static class IntentListCursorCodec
{
    public static string? Encode(IntentListCursor? cursor)
    {
        if (cursor is null)
        {
            return null;
        }
        var iso = cursor.SortValue.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        var raw = $"{iso}|{cursor.Id}|{cursor.SortKey}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    public static IntentListCursor? Parse(string? cursor)
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
        if (parts.Length < 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
        {
            throw new ArgumentException("cursor payload is malformed.", nameof(cursor));
        }

        if (!DateTimeOffset.TryParse(
                parts[0],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var sortValue))
        {
            throw new ArgumentException("cursor sort-value is not a valid timestamp.", nameof(cursor));
        }

        var sortKey = parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : null;
        return new IntentListCursor(sortValue, parts[1], sortKey);
    }
}
