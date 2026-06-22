using Throne.Domain.Capabilities;

namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Static metadata for every carrier capability (a feature with multiple interchangeable
/// providers). Description copy + provider list ships in code so changing the surface is a
/// deploy, not a migration; only the operator's <c>selected_provider</c> is persisted.
/// </summary>
public static class CapabilityCatalog
{
    public static readonly IReadOnlyList<CapabilityDescriptor> Descriptors =
    [
        new CapabilityDescriptor(
            Name: CapabilityNames.OpenInIde,
            Title: "Открыть в IDE",
            Description: "Кнопки «Открыть в IDE» на странице интента и на каждом репо-биндинге. Открывает workspace интента или конкретный clone в выбранной IDE.",
            Providers:
            [
                new CapabilityProviderDescriptor(
                    Name: "vscode",
                    Title: "VS Code",
                    PrerequisiteHint: "VS Code → Command Palette → Shell Command: Install 'code' command in PATH"),
                new CapabilityProviderDescriptor(
                    Name: "cursor",
                    Title: "Cursor",
                    PrerequisiteHint: "Cursor → Command Palette → Shell Command: Install 'cursor' command in PATH"),
            ]),
    ];

    public static CapabilityDescriptor? FindByName(string name) =>
        Descriptors.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
}

public sealed record CapabilityDescriptor(
    string Name,
    string Title,
    string Description,
    IReadOnlyList<CapabilityProviderDescriptor> Providers)
{
    public CapabilityProviderDescriptor? FindProvider(string providerName) =>
        Providers.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.Ordinal));
}

public sealed record CapabilityProviderDescriptor(
    string Name,
    string Title,
    string PrerequisiteHint);
