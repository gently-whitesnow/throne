using System.Collections.Concurrent;

namespace Throne.Application.Terminals;

/// <summary>
/// Submit-side sibling of <see cref="TerminalReadinessSignals"/>: a per-intent latch the prompt
/// confirmer waits on, fired by the agent's <c>UserPromptSubmit</c> hook callback. The hook means
/// "the agent accepted a user prompt", so it is an authoritative submit confirmation — strictly more
/// reliable than scraping a working footer off the pane, which Claude muddies by echoing the pasted
/// prompt back into the transcript. Kept as its own registry (not folded into the readiness one) so
/// the two latches never collide on the shared intent id: readiness fires on <c>SessionReady</c>,
/// submit on <c>UserPromptSubmit</c>, and both can be armed for the same intent at once.
/// </summary>
public sealed class TerminalPromptSubmitSignals
{
    private readonly ConcurrentDictionary<string, SubmitSignal> _signals =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Latches a submit signal for <paramref name="intentId"/>. Arm before the trailing Enter so a
    /// fast hook callback is not lost in the gap. Dispose the registration once the confirm is done.
    /// </summary>
    public SubmitRegistration Arm(string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        var signal = new SubmitSignal();
        _signals.AddOrUpdate(intentId, signal, (_, _) => signal);
        return new SubmitRegistration(intentId, signal, this);
    }

    public bool TrySignal(string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        return _signals.TryGetValue(intentId, out var signal) && signal.TrySetSubmitted();
    }

    private void Release(string intentId, SubmitSignal signal)
    {
        if (_signals.TryGetValue(intentId, out var current) && ReferenceEquals(current, signal))
        {
            _signals.TryRemove(intentId, out _);
        }
    }

    internal sealed class SubmitSignal
    {
        private readonly TaskCompletionSource _submitted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Submitted => _submitted.Task;

        public bool TrySetSubmitted() => _submitted.TrySetResult();
    }

    public sealed class SubmitRegistration : IDisposable
    {
        private readonly TerminalPromptSubmitSignals _owner;
        private readonly SubmitSignal _signal;
        private bool _disposed;

        internal SubmitRegistration(string intentId, SubmitSignal signal, TerminalPromptSubmitSignals owner)
        {
            IntentId = intentId;
            _signal = signal;
            _owner = owner;
        }

        public string IntentId { get; }

        public Task Submitted => _signal.Submitted;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _owner.Release(IntentId, _signal);
            _disposed = true;
        }
    }
}
