namespace Throne.Infrastructure.Imaging;

public sealed class IntentAttachmentCompressionOptions
{
    public const string SectionName = "Throne:IntentAttachmentCompression";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    public int BatchSize { get; set; } = 20;

    public int MaxDimension { get; set; } = 768;

    public int JpegQuality { get; set; } = 60;
}
