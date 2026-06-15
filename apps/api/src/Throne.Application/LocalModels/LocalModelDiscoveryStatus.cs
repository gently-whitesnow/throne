namespace Throne.Application.LocalModels;

/// <summary>
/// Health state of local-model discovery, mirrored 1:1 by the wire enum.
/// </summary>
public enum LocalModelDiscoveryStatus
{
    /// <summary><c>Throne:LocalModel:BaseUrl</c> is empty — nothing was probed.</summary>
    NotConfigured,

    /// <summary>The endpoint answered and advertises at least one model.</summary>
    Ready,

    /// <summary>The endpoint answered but advertises no models.</summary>
    Empty,

    /// <summary>The endpoint could not be reached or returned an unusable response.</summary>
    Unreachable,
}
