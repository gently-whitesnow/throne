namespace Throne.Domain.Instructions;

public readonly record struct InstructionId(string Value)
{
    public static InstructionId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
