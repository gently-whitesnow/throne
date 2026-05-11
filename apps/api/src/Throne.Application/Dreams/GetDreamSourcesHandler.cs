using Throne.Application.Instructions.Manifest;

namespace Throne.Application.Dreams;

/// <summary>
/// Returns the list of <c>dream_sources</c> declared in the skill manifest —
/// where the frontier agent should look for prior conversations locally during
/// a /dream pass. Sources are global in v1 (no per-user override); the server
/// itself never reads these paths.
/// </summary>
public sealed class GetDreamSourcesHandler(ISkillManifestProvider manifestProvider)
{
    public IReadOnlyList<DreamSourceEntry> Handle()
    {
        var sources = manifestProvider.Current.DreamSources;
        var result = new List<DreamSourceEntry>(sources.Count);
        foreach (var s in sources)
        {
            result.Add(new DreamSourceEntry(s.Vendor, s.Path, s.Hint));
        }
        return result;
    }
}
