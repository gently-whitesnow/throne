namespace Throne.Application.Dreams;

/// <summary>
/// Result row of <see cref="GetDreamSourcesHandler"/>. Mirrors
/// <c>DreamSourceManifestEntry</c> but stays in the Application surface so
/// callers do not reach into the Manifest namespace.
/// </summary>
public sealed record DreamSourceEntry(string Vendor, string Path, string Hint);
