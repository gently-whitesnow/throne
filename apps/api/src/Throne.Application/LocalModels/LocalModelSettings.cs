namespace Throne.Application.LocalModels;

/// <summary>
/// Address of a local OpenAI-compatible endpoint whose models are discovered via
/// <c>GET {BaseUrl}/v1/models</c>. Config-bound: the operator sets it through
/// <c>Throne:LocalModel:BaseUrl</c>, never persisted at runtime. Empty/whitespace means
/// "not configured" — discovery reports a controlled fallback instead of probing.
/// </summary>
public sealed class LocalModelSettings
{
    public const string SectionName = "Throne:LocalModel";

    public string? BaseUrl { get; set; }
}
