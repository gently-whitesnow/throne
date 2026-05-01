using MongoDB.Driver;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoSessionAccessor
{
    private readonly AsyncLocal<IClientSessionHandle?> _current = new();

    public IClientSessionHandle? Current => _current.Value;

    public IDisposable BeginScope(IClientSessionHandle session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_current.Value is not null)
        {
            throw new InvalidOperationException("Nested Mongo session scopes are not supported.");
        }

        _current.Value = session;
        return new Scope(this);
    }

    private sealed class Scope(MongoSessionAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor._current.Value = null;
    }
}
