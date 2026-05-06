namespace Throne.Application.Intents.Attachments;

public enum AttachmentKind
{
    Unsupported = 0,
    Image,
    Text,
}

public static class AttachmentKindResolver
{
    public const string ToolNameImage = "read_intent_attachment_image";
    public const string ToolNameText = "read_intent_attachment_text";

    public static AttachmentKind Resolve(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return AttachmentKind.Unsupported;
        }

        var slash = contentType.IndexOf('/');
        var primary = slash > 0 ? contentType[..slash] : contentType;
        var trimmed = contentType.Trim();

        if (string.Equals(primary, "image", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentKind.Image;
        }

        if (string.Equals(primary, "text", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentKind.Text;
        }

        if (TextSubtypes.Contains(trimmed))
        {
            return AttachmentKind.Text;
        }

        return AttachmentKind.Unsupported;
    }

    public static string? RecommendedTool(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Image => ToolNameImage,
        AttachmentKind.Text => ToolNameText,
        _ => null,
    };

    public static string KindWireName(AttachmentKind kind) => kind switch
    {
        AttachmentKind.Image => "image",
        AttachmentKind.Text => "text",
        _ => "unsupported",
    };

    private static readonly HashSet<string> TextSubtypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/x-ndjson",
        "application/xml",
        "application/x-yaml",
        "application/yaml",
    };
}
