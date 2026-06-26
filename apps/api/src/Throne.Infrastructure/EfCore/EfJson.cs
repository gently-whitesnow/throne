using System.Text.Json;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for every <c>HasConversion</c> that stores
/// a complex value as a JSON string column. The configuration is intentionally minimal —
/// camelCase off (column shapes stay snake_case-agnostic), no indenting (no whitespace
/// overhead), no nullable surprises — so that round-trips are byte-stable.
/// </summary>
internal static class EfJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
