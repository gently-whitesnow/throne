namespace Throne.Domain.Dreams;

/// <summary>
/// Factory façade for <see cref="DreamSession"/>. Lives outside the aggregate
/// so the aggregate's per-type cyclomatic complexity stays inside the
/// project's CA1502 budget — guard branching is moved out of the class.
/// </summary>
public static class DreamSessionFactory
{
    public static DreamSession Create(DreamSessionCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        DreamSessionGuards.EnsureCreateInputs(
            input.Id,
            input.OwnerUserId,
            input.Vendor,
            input.DateFrom,
            input.DateTo,
            input.ProcessedConversationIds,
            input.Summary,
            input.Reflection,
            input.ProposedPatchIds);
        return Materialise(input, vendor: input.Vendor.Trim());
    }

    public static DreamSession Restore(DreamSessionCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OwnerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Vendor);
        ArgumentNullException.ThrowIfNull(input.ProcessedConversationIds);
        ArgumentNullException.ThrowIfNull(input.ProposedPatchIds);
        ArgumentNullException.ThrowIfNull(input.Summary);
        return Materialise(input, vendor: input.Vendor);
    }

    private static DreamSession Materialise(DreamSessionCreateInput input, string vendor) =>
        new(
            new DreamSessionIdentity(input.Id, input.OwnerUserId, input.CreatedAt),
            new DreamSessionPayload(
                vendor,
                input.DateFrom,
                input.DateTo,
                [.. input.ProcessedConversationIds],
                input.Summary,
                input.Reflection,
                [.. input.ProposedPatchIds]));
}
