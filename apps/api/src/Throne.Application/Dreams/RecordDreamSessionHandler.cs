using Throne.Application.Errors;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Dreams;

namespace Throne.Application.Dreams;

public sealed record RecordDreamSessionCommand(
    string Vendor,
    string Host,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    IReadOnlyList<string> ProcessedConversationIds,
    string Summary,
    string? Reflection,
    IReadOnlyList<string> ProposedPatchIds);

/// <summary>
/// Frontier-agent surface for appending a <see cref="DreamSession"/> at the
/// end of a /dream pass. Records are immutable — no update path is exposed
/// (intentionally; corrections happen by recording a fresh session).
///
/// Vendor is validated against the <c>dream_sources</c> manifest (the same
/// list surfaced by <see cref="GetDreamSourcesHandler"/>) so callers cannot
/// silently invent vendors that never had a path declared.
/// </summary>
public sealed class RecordDreamSessionHandler(
    IDreamSessionRepository sessions,
    IUnitOfWork unitOfWork,
    ISkillManifestProvider manifestProvider,
    TimeProvider clock)
{
    public async Task<DreamSession> HandleAsync(RecordDreamSessionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        DreamSessionVendorValidator.Ensure(command.Vendor, manifestProvider.Current.DreamSources);
        var session = BuildOrThrow(command);
        var outcome = await unitOfWork.ExecuteAsync(
            inner => sessions.CreateAsync(session, inner),
            ct);
        return outcome.Session;
    }

    private DreamSession BuildOrThrow(RecordDreamSessionCommand command)
    {
        try
        {
            return DreamSession.Create(
                id: Guid.NewGuid().ToString("N"),
                createdAt: clock.GetUtcNow(),
                vendor: command.Vendor,
                host: command.Host,
                dateFrom: command.DateFrom,
                dateTo: command.DateTo,
                processedConversationIds: command.ProcessedConversationIds ?? [],
                summary: command.Summary ?? string.Empty,
                reflection: NullIfEmpty(command.Reflection),
                proposedPatchIds: command.ProposedPatchIds ?? []);
        }
        catch (ArgumentException ex)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                ex.Message,
                new Dictionary<string, object?> { ["field"] = ex.ParamName ?? "dream_session" });
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// Application-side check that the caller's vendor matches one of the manifest
/// <c>dream_sources</c> entries. Sits outside the handler so the handler's
/// per-method cyclomatic complexity stays inside CA1502.
/// </summary>
internal static class DreamSessionVendorValidator
{
    public static void Ensure(string vendor, IReadOnlyList<DreamSourceManifestEntry> known)
    {
        if (string.IsNullOrWhiteSpace(vendor))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "vendor must not be empty.",
                new Dictionary<string, object?> { ["field"] = "vendor" });
        }
        if (known.Count == 0)
        {
            // No manifest constraint configured (tests / dev environments) —
            // accept any non-empty vendor; the domain guard bounds its length.
            return;
        }
        if (Contains(known, vendor))
        {
            return;
        }
        throw new ApiException(
            ErrorCodes.ValidationFailed,
            $"Unknown vendor '{vendor}'. Known vendors come from get_dream_sources.",
            new Dictionary<string, object?> { ["field"] = "vendor" });
    }

    private static bool Contains(IReadOnlyList<DreamSourceManifestEntry> known, string vendor)
    {
        foreach (var s in known)
        {
            if (string.Equals(s.Vendor, vendor, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
