namespace Throne.Application.LocalModels;

/// <summary>
/// Controlled outcome of a local-model discovery probe. The status is the single axis the UI
/// branches on; <see cref="Models"/> is meaningful only for <see cref="LocalModelDiscoveryStatus.Ready"/>
/// and <see cref="Error"/> only for <see cref="LocalModelDiscoveryStatus.Unreachable"/>.
/// </summary>
public sealed record LocalModelDiscoveryResult(
    LocalModelDiscoveryStatus Status,
    string? BaseUrl,
    IReadOnlyList<string> Models,
    string? Error)
{
    private static readonly IReadOnlyList<string> Empty = Array.Empty<string>();

    public static LocalModelDiscoveryResult NotConfigured() =>
        new(LocalModelDiscoveryStatus.NotConfigured, null, Empty, null);

    public static LocalModelDiscoveryResult Ready(string baseUrl, IReadOnlyList<string> models) =>
        new(LocalModelDiscoveryStatus.Ready, baseUrl, models, null);

    public static LocalModelDiscoveryResult EmptyCatalog(string baseUrl) =>
        new(LocalModelDiscoveryStatus.Empty, baseUrl, Empty, null);

    public static LocalModelDiscoveryResult Unreachable(string baseUrl, string error) =>
        new(LocalModelDiscoveryStatus.Unreachable, baseUrl, Empty, error);
}
