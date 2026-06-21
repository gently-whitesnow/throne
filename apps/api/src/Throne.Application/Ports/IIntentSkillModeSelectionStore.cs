namespace Throne.Application.Ports;

public interface IIntentSkillModeSelectionStore
{
    Task<IReadOnlyList<string>?> GetAsync(string intentId, string mode, CancellationToken ct);

    Task SaveAsync(string intentId, string mode, IReadOnlyList<string> selectedSkillIds, CancellationToken ct);
}
