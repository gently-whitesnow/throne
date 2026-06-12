using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// Reads the single <see cref="PromptPart"/> for a given <c>(scope, key)</c> target.
/// Encapsulates the repository lookup so the patch handlers don't repeat it.
/// </summary>
public sealed class UserPromptPartLookup(IPromptPartRepository promptParts)
{
    public Task<PromptPart?> FindAsync(string scope, string key, CancellationToken ct) =>
        promptParts.GetByScopeKeyAsync(scope, key, ct);
}
