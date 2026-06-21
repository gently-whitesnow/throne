using Throne.Application.Terminals;

namespace Throne.Application.Ports;

public interface ISkillModeDefaultStore
{
    Task<IReadOnlyList<SkillModeDefault>> ListAsync(CancellationToken ct);

    Task ReplaceAsync(IReadOnlyList<SkillModeDefault> defaults, CancellationToken ct);

    Task UpsertMissingAsync(IReadOnlyList<SkillModeDefault> defaults, CancellationToken ct);
}
