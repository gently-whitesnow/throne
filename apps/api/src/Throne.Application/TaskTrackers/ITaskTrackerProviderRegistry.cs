namespace Throne.Application.TaskTrackers;

/// <summary>
/// Resolves the right <see cref="ITaskTrackerProvider"/> for a given provider key, mirroring
/// <c>IGitProviderRegistry</c>. No provider is registered in the provider-neutral core; adapters
/// plug in with one <c>AddSingleton&lt;ITaskTrackerProvider, …&gt;()</c> line without touching this
/// surface.
/// </summary>
public interface ITaskTrackerProviderRegistry
{
    /// <summary>
    /// Return the implementation for <paramref name="trackerKey"/> or <see langword="null"/> when no
    /// provider is registered under that key. Callers translate <see langword="null"/> into a 422
    /// (unsupported tracker) at the server boundary per ADR-0046.
    /// </summary>
    ITaskTrackerProvider? GetByName(string trackerKey);

    /// <summary>
    /// All providers known to the host process, in registration order — backs the catalog endpoint.
    /// </summary>
    IReadOnlyCollection<ITaskTrackerProvider> AllProviders { get; }
}
