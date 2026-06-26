namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Ambient <see cref="ThroneDbContext"/> for the current async flow. A unit of work opens
/// exactly one context, publishes it here for the duration of the work, and repositories
/// pick it up transparently.
/// Nested scopes are rejected: a reentrant <c>ExecuteAsync</c> must join the ambient
/// context, not open a second one.
/// </summary>
internal sealed class EfSessionAccessor
{
    private readonly AsyncLocal<ThroneDbContext?> _current = new();

    public ThroneDbContext? Current => _current.Value;

    public IDisposable BeginScope(ThroneDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_current.Value is not null)
        {
            throw new InvalidOperationException("Nested EF Core context scopes are not supported.");
        }

        _current.Value = context;
        return new Scope(this);
    }

    private sealed class Scope(EfSessionAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor._current.Value = null;
    }
}
