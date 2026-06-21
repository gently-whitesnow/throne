namespace Throne.Application.Terminals;

public sealed record SkillModeDefault(string Mode, string SkillId, bool Enabled);

public sealed record SkillModeDefaultState(string SkillId, bool Enabled);

public sealed record SkillModeDefaultsModeView(
    string Mode,
    IReadOnlyList<SkillModeDefaultState> Defaults);

public sealed record SkillModeDefaultsView(
    IReadOnlyList<SessionSkillDescriptor> Skills,
    IReadOnlyList<SkillModeDefaultsModeView> Modes);

public sealed record AvailableSessionSkill(
    string SkillId,
    string Source,
    string Title,
    string Description,
    bool Materializable,
    string? Reason,
    bool DefaultEnabled,
    bool Selected);

public sealed record SessionSkillRunSelection(
    IReadOnlyList<string> SelectedSkillIds,
    ReviewArtifactWriteTarget? ReviewArtifact);
