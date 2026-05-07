namespace Throne.Domain;

/// <summary>
/// Stable assembly anchor for architecture tests. Reference this type instead of
/// concrete domain types so renames in the domain model do not break
/// <c>Throne.Architecture.Tests</c>.
/// </summary>
public sealed class AssemblyMarker;
