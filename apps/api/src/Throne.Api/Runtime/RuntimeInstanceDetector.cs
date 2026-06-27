namespace Throne.Api.Runtime;

internal static class RuntimeInstanceDetector
{
    public const string EphemeralEnvVar = "THRONE_EPHEMERAL";

    public static bool IsEphemeral(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (IsTruthy(Environment.GetEnvironmentVariable(EphemeralEnvVar)))
        {
            return true;
        }

        return LooksInsideIntentWorkspace(Environment.GetEnvironmentVariable("THRONE_HOME"))
            || LooksInsideIntentWorkspace(configuration["Persistence:Sqlite:DataSource"])
            || LooksInsideIntentWorkspace(configuration["Throne:Workspace:Root"]);
    }

    internal static bool LooksInsideIntentWorkspace(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var parts = fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "intents", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(parts[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTruthy(string? value) =>
        value is not null
        && (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));
}
