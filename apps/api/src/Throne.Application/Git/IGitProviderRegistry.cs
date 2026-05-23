namespace Throne.Application.Git;

/// <summary>
/// Resolves the right <see cref="IGitProvider"/> for a given provider key.
/// Slice 1 registers only <c>github</c>; slice 5 adds <c>gitlab</c> without
/// touching the registry's public surface.
/// </summary>
public interface IGitProviderRegistry
{
    /// <summary>
    /// Return the implementation for <paramref name="providerName"/> or
    /// <see langword="null"/> when no provider is registered under that key.
    /// Callers translate <see langword="null"/> into a 422 (unsupported provider)
    /// per the OpenAPI contract.
    /// </summary>
    IGitProvider? GetByName(string providerName);

    /// <summary>
    /// All providers known to the host process. Used by the settings page to
    /// render one auth-status row per provider without hard-coding the list.
    /// </summary>
    IReadOnlyCollection<IGitProvider> AllProviders { get; }
}
