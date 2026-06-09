using Throne.Application.Ports;

namespace Throne.Application.Terminals;

/// <summary>
/// Read/write the operator-controlled default terminal vendor. The write path is the only
/// mutator and runs inside a unit of work so the settings singleton upserts atomically.
/// </summary>
public sealed class TerminalSettingsService(ITerminalSettingsStore store, IUnitOfWork unitOfWork)
{
    public Task<string> GetDefaultVendorAsync(CancellationToken ct) =>
        store.GetDefaultVendorAsync(ct);

    public async Task<string> SetDefaultVendorAsync(string vendor, CancellationToken ct)
    {
        if (!TerminalAgentCatalog.IsKnownVendor(vendor))
        {
            throw TerminalFailures.VendorInvalid(vendor);
        }

        await unitOfWork.ExecuteAsync(inner => store.SetDefaultVendorAsync(vendor, inner), ct);
        return vendor;
    }
}
