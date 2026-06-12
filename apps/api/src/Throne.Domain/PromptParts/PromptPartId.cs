namespace Throne.Domain.PromptParts;

public readonly record struct PromptPartId(string Value)
{
    public static PromptPartId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
