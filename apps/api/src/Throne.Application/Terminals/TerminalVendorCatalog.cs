namespace Throne.Application.Terminals;

/// <summary>
/// Default <see cref="ITerminalVendorCatalog"/>. Indexes the DI-registered descriptors by
/// <see cref="TerminalVendorDescriptor.Vendor"/> at construction time for O(1) lookup while
/// preserving registration order for the display list — same shape as <c>GitProviderRegistry</c>.
/// </summary>
public sealed class TerminalVendorCatalog : ITerminalVendorCatalog
{
    private readonly Dictionary<string, TerminalVendorDescriptor> _byVendor;

    public TerminalVendorCatalog(IEnumerable<TerminalVendorDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var ordered = new List<TerminalVendorDescriptor>();
        var map = new Dictionary<string, TerminalVendorDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (!map.TryAdd(descriptor.Vendor, descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate TerminalVendorDescriptor registration for '{descriptor.Vendor}'.");
            }
            ordered.Add(descriptor);
        }

        Descriptors = ordered;
        _byVendor = map;
    }

    public IReadOnlyList<TerminalVendorDescriptor> Descriptors { get; }

    public TerminalVendorDescriptor? Find(string vendor) =>
        string.IsNullOrEmpty(vendor) ? null : _byVendor.GetValueOrDefault(vendor);

    public TerminalVendorDescriptor DescriptorFor(string vendor) =>
        Find(vendor)
        ?? throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'.");

    public bool IsKnownVendor(string vendor) =>
        !string.IsNullOrEmpty(vendor) && _byVendor.ContainsKey(vendor);
}
