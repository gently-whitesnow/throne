namespace Throne.Application.Terminals;

/// <summary>
/// Resolves the right <see cref="TerminalVendorDescriptor"/> for a given vendor token and
/// exposes the registered vendors in display order. Mirrors <c>IGitProviderRegistry</c>: the
/// descriptors are DI-registered (<c>IEnumerable&lt;TerminalVendorDescriptor&gt;</c>) and indexed
/// by <see cref="TerminalVendorDescriptor.Vendor"/>, so a new vendor plugs in without editing a
/// central list (ADR-0045).
/// </summary>
public interface ITerminalVendorCatalog
{
    /// <summary>Descriptors in registration (display) order; drives the launch-surface dropdown.</summary>
    IReadOnlyList<TerminalVendorDescriptor> Descriptors { get; }

    /// <summary>The descriptor for <paramref name="vendor"/>, or <see langword="null"/> if unknown.</summary>
    TerminalVendorDescriptor? Find(string vendor);

    /// <summary>The descriptor for <paramref name="vendor"/>; throws if the vendor is not registered.</summary>
    TerminalVendorDescriptor DescriptorFor(string vendor);

    bool IsKnownVendor(string vendor);
}
