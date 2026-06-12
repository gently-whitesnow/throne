namespace Throne.Domain.PromptParts;

/// <summary>Role + sort position of a <see cref="PromptPart"/> within one embedded mode.</summary>
public sealed record PromptPartModeRole(string Mode, string Role, int Order);
