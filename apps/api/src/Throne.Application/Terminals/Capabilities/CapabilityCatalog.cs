using Throne.Domain.Capabilities;

namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Static metadata for every capability known to Slice 2 (`title`, `description`,
/// `prerequisite_hint`). Detection (`detected` / `detection_detail`) and the persisted
/// toggle (`enabled`) are resolved at runtime — see <see cref="CapabilitiesService"/>.
///
/// The catalog stays in code rather than Mongo: descriptions are operator-facing UI
/// strings, not user data, and shipping them with the build keeps the
/// `GET /api/v1/settings/capabilities` surface stable without a migration step.
/// Order matches the order surfaced in `/settings` (ADR-0026 § 1).
/// </summary>
public static class CapabilityCatalog
{
    public static readonly IReadOnlyList<CapabilityDescriptor> Descriptors =
    [
        new CapabilityDescriptor(
            Name: CapabilityNames.Repositories,
            Title: "Репозитории GitHub/GitLab",
            Description: "Привязка репозиториев к интенту, авто-клон в workspace, секция PR-комментариев. GitLab включается отдельным opt-in.",
            PrerequisiteHint: "gh auth login"),

        new CapabilityDescriptor(
            Name: CapabilityNames.Gitlab,
            Title: "GitLab provider",
            Description: "Второй git-провайдер через локальный `glab` и настроенный `Throne:GitLab:Host`. Требует также capability `repositories`.",
            PrerequisiteHint: "glab auth login --hostname <host>"),

        new CapabilityDescriptor(
            Name: CapabilityNames.Terminal,
            Title: "Терминал агента",
            Description: "Кнопка Run и встроенный xterm на странице интента. Сессия живёт в tmux и переживает рестарт Throne.",
            PrerequisiteHint: "brew install tmux"),

        new CapabilityDescriptor(
            Name: CapabilityNames.Vscode,
            Title: "Open in VS Code",
            Description: "Кнопки «Open in VS Code» на странице интента и на каждом репо-биндинге. Открывает workspace интента или конкретный clone.",
            PrerequisiteHint: "VS Code → Command Palette → Shell Command: Install 'code' command in PATH"),

        new CapabilityDescriptor(
            Name: CapabilityNames.Opencode,
            Title: "OpenCode vendor",
            Description: "Запуск OpenCode CLI в терминале интента с моделями из локального OpenAI-совместимого эндпоинта. Требует также capability `terminal`.",
            PrerequisiteHint: "npm i -g opencode-ai && настроить Throne:LocalModel:BaseUrl"),
    ];

    public static CapabilityDescriptor? FindByName(string name) =>
        Descriptors.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// Operator-facing metadata for one capability. Combined with the persisted toggle
/// (<see cref="Capabilities"/>) and the live probe result (<see cref="ICapabilityDetectionCache"/>)
/// when materialising <c>CapabilityDto</c>.
/// </summary>
public sealed record CapabilityDescriptor(
    string Name,
    string Title,
    string Description,
    string PrerequisiteHint);
