namespace Throne.Domain.Instructions;

public sealed class Instruction
{
    internal Instruction(
        InstructionId id,
        InstructionDescriptor descriptor,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Descriptor = descriptor;
        Text = text;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public InstructionId Id { get; }
    public InstructionDescriptor Descriptor { get; }
    public string Text { get; internal set; }
    public int CurrentVersion { get; internal set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; internal set; }
}
