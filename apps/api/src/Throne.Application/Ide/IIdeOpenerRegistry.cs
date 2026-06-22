namespace Throne.Application.Ide;

/// <summary>
/// Closed registry of IDE-opener providers known to this build. Populated by DI from
/// every <see cref="IIdeOpener"/> registration.
/// </summary>
public interface IIdeOpenerRegistry
{
    IReadOnlyList<IIdeOpener> All { get; }

    IIdeOpener? Find(string providerName);
}

public sealed class IdeOpenerRegistry : IIdeOpenerRegistry
{
    private readonly Dictionary<string, IIdeOpener> _byName;

    public IdeOpenerRegistry(IEnumerable<IIdeOpener> openers)
    {
        var list = openers.ToList();
        All = list;
        _byName = list.ToDictionary(o => o.ProviderName, StringComparer.Ordinal);
    }

    public IReadOnlyList<IIdeOpener> All { get; }

    public IIdeOpener? Find(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }
        return _byName.TryGetValue(providerName, out var opener) ? opener : null;
    }
}
