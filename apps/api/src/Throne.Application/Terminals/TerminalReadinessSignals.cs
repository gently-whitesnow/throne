using System.Collections.Concurrent;

namespace Throne.Application.Terminals;

public sealed class TerminalReadinessSignals
{
    private readonly ConcurrentDictionary<string, TerminalReadinessSignal> _signals =
        new(StringComparer.Ordinal);

    public TerminalReadinessRegistration Arm(string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        var signal = new TerminalReadinessSignal();
        _signals.AddOrUpdate(intentId, signal, (_, _) => signal);
        return new TerminalReadinessRegistration(intentId, signal, this);
    }

    public bool TrySignal(string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        return _signals.TryGetValue(intentId, out var signal)
            && signal.TrySetReady();
    }

    private void Release(string intentId, TerminalReadinessSignal signal)
    {
        if (_signals.TryGetValue(intentId, out var current) && ReferenceEquals(current, signal))
        {
            _signals.TryRemove(intentId, out _);
        }
    }

    internal sealed class TerminalReadinessSignal
    {
        private readonly TaskCompletionSource _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Ready => _ready.Task;

        public bool TrySetReady() => _ready.TrySetResult();
    }

    public sealed class TerminalReadinessRegistration : IDisposable
    {
        private readonly TerminalReadinessSignals _owner;
        private readonly TerminalReadinessSignal _signal;
        private bool _disposed;

        internal TerminalReadinessRegistration(
            string intentId,
            TerminalReadinessSignal signal,
            TerminalReadinessSignals owner)
        {
            IntentId = intentId;
            _signal = signal;
            _owner = owner;
        }

        public string IntentId { get; }

        public Task Ready => _signal.Ready;

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
