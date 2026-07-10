using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Throne.Application.Terminals;

/// <summary>
/// Curated per-vendor model whitelist loaded once from the embedded
/// <c>vendor-models.json</c>. Chosen over inline arrays in
/// <see cref="TerminalVendorDescriptors"/> so that adding/removing a vendor model is a PR
/// against one data file, not a .cs edit (ADR-0054). Order in the JSON is native-default-first
/// and is preserved verbatim — the first entry becomes each descriptor's <c>DefaultModel</c>.
/// The list is not a network probe: for provenance-sensitive live sources (OpenCode) the
/// descriptor still opts into <see cref="TerminalAgentCatalog.ModelSourceLocal"/>.
/// </summary>
public static class VendorModelMetadata
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Loaded =
        new(LoadFromEmbeddedResource, isThreadSafe: true);

    /// <summary>Model whitelist for the given vendor token; empty when the file lists none.</summary>
    public static IReadOnlyList<string> For(string vendor) =>
        Loaded.Value.TryGetValue(vendor, out var models)
            ? models
            : Array.Empty<string>();

    // Exposed for unit tests that want to bypass the embedded stream.
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Parse(string payload)
    {
        var parsed = JsonSerializer.Deserialize<VendorModelsFile>(payload, JsonOptions)
            ?? throw new InvalidOperationException("vendor-models.json parsed to null.");
        if (parsed.Vendors is null || parsed.Vendors.Count == 0)
        {
            throw new InvalidOperationException("vendor-models.json: 'vendors' section empty.");
        }
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (vendor, models) in parsed.Vendors)
        {
            if (string.IsNullOrEmpty(vendor))
            {
                throw new InvalidOperationException("vendor-models.json: empty vendor key.");
            }
            if (models is null || models.Count == 0)
            {
                throw new InvalidOperationException(
                    $"vendor-models.json: vendor '{vendor}' has empty model list.");
            }
            foreach (var id in models)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidOperationException(
                        $"vendor-models.json: vendor '{vendor}' has a blank model id.");
                }
            }
            result[vendor] = models.ToArray();
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadFromEmbeddedResource()
    {
        var assembly = typeof(VendorModelMetadata).Assembly;
        var resourceName = ResolveResourceName(assembly);
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' missing — check .csproj EmbeddedResource item.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static string ResolveResourceName(Assembly assembly)
    {
        // MSBuild namespaces embedded resources as `<AssemblyName>.<PathWithDots>` — encode the
        // filename here rather than iterating manifest names so a typo fails fast.
        return $"{assembly.GetName().Name}.Terminals.vendor-models.json";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed record VendorModelsFile(
        [property: JsonPropertyName("vendors")] IReadOnlyDictionary<string, IReadOnlyList<string>>? Vendors);
}
