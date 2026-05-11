using System.Globalization;
using System.Text;

namespace Throne.Infrastructure.Mongo;

internal static class IntentLinkCursor
{
    public static string Encode(DateTime createdAt, string id)
    {
        var iso = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
            .ToString("o", CultureInfo.InvariantCulture);
        var raw = $"{iso}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static (DateTime CreatedAt, string Id) Decode(string cursor)
    {
        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
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
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            throw new ArgumentException("cursor created_at is not a valid timestamp.", nameof(cursor));
        }

        return (dto.UtcDateTime, parts[1]);
    }
}
