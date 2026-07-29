using System.Globalization;

namespace Throne.Api.Cli;

/// <summary>
/// Renders the age of a running instance as a compact, human-readable string for
/// <c>throne status</c>. The two coarsest non-zero units are kept ("2d 4h", "13m 07s")
/// so the line stays scannable in a terminal.
/// </summary>
internal static class UptimeFormat
{
    public static string Describe(DateTimeOffset startedAt, DateTimeOffset now)
    {
        var elapsed = now - startedAt;

        if (elapsed.TotalDays >= 1)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{elapsed.Days}d {elapsed.Hours}h");
        }

        if (elapsed.TotalHours >= 1)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{elapsed.Hours}h {elapsed.Minutes:D2}m");
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{elapsed.Minutes}m {elapsed.Seconds:D2}s");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{elapsed.Seconds}s");
    }
}
