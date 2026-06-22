namespace Throne.Application.Terminals;

/// <summary>
/// Vendor-aware facade over <see cref="TmuxPromptSubmitConfirmer"/>: looks up the hook adapter by
/// vendor, runs the bracketed-paste submit confirmation, and raises
/// <see cref="TerminalFailures.PromptSubmitNotConfirmed"/> on timeout. Callers that already hold an
/// adapter use the overload that takes one directly.
/// </summary>
public sealed class TmuxPromptSubmitGate
{
    private readonly Dictionary<string, ISessionHookAdapter> _hookAdapters;
    private readonly TmuxPromptSubmitConfirmer _confirmer;
    private readonly RunPreflightOptions _options;

    public TmuxPromptSubmitGate(
        IEnumerable<ISessionHookAdapter> hookAdapters,
        TmuxPromptSubmitConfirmer confirmer,
        RunPreflightOptions options)
    {
        _hookAdapters = hookAdapters.ToDictionary(a => a.Vendor, StringComparer.Ordinal);
        _confirmer = confirmer;
        _options = options;
    }

    public async Task<bool> ConfirmOrThrowAsync(string intentId, string vendor, string? submittedPrompt, CancellationToken ct)
    {
        if (!_hookAdapters.TryGetValue(vendor, out var adapter))
        {
            return false;
        }
        return await ConfirmOrThrowAsync(intentId, vendor, adapter, submittedPrompt, ct);
    }

    public async Task<bool> ConfirmOrThrowAsync(
        string intentId, string vendor, ISessionHookAdapter adapter, string? submittedPrompt, CancellationToken ct)
    {
        var confirmation = await _confirmer.ConfirmAsync(intentId, adapter, submittedPrompt, ct);
        if (!confirmation.IsConfirmed)
        {
            throw TerminalFailures.PromptSubmitNotConfirmed(
                intentId,
                vendor,
                confirmation.Retries,
                _options.PromptSubmitConfirmTimeoutMilliseconds,
                confirmation.LastSnapshot);
        }
        return true;
    }
}
