using System.Reflection;

namespace Throne.Api;

/// <summary>
/// Single source of truth for the running build's version. Sourced from the
/// assembly's informational version (set by MSBuild <c>Version</c> / the release
/// tag), consumed by the <c>/version</c> endpoint and <c>throne update</c> to
/// decide whether a newer GitHub release exists.
/// </summary>
public static class ThroneVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var informational = typeof(ThroneVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // MSBuild appends "+<commit-sha>" (SourceRevisionId); drop it.
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? informational[..plus] : informational;
        }

        return typeof(ThroneVersion).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
