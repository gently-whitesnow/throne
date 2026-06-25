namespace Throne.Application.Terminals;

public interface ITerminalOpenerRegistry
{
    IReadOnlyList<ITerminalOpener> All { get; }

    ITerminalOpener? Find(string providerName);
}

public sealed class TerminalOpenerRegistry : ITerminalOpenerRegistry
{
    private readonly Dictionary<string, ITerminalOpener> _byName;

    public TerminalOpenerRegistry(IEnumerable<ITerminalOpener> openers)
    {
        var list = openers.ToList();
        All = list;
        _byName = list.ToDictionary(o => o.ProviderName, StringComparer.Ordinal);
    }

    public IReadOnlyList<ITerminalOpener> All { get; }

    public ITerminalOpener? Find(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }
        return _byName.TryGetValue(providerName, out var opener) ? opener : null;
    }
}
