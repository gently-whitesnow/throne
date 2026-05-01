namespace Throne.Domain.Instructions;

public sealed class Instruction
{
    private Instruction(
        InstructionId id,
        string kind,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Kind = kind;
        Text = text;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public InstructionId Id { get; }
    public string Kind { get; }
    public string Text { get; }
    public int CurrentVersion { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static Instruction Create(
        InstructionId id,
        string kind,
        string text,
        DateTimeOffset now)
    {
        Validate(kind, text);
        return new Instruction(id, kind, text, currentVersion: 1, now, now);
    }

    public static Instruction Restore(
        InstructionId id,
        string kind,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Validate(kind, text);
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        return new Instruction(id, kind, text, currentVersion, createdAt, updatedAt);
    }

    private static void Validate(string kind, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(text);

        if (!InstructionKindNames.IsKnown(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), $"Unknown instruction kind: {kind}.");
        }

        if (text.Length == 0)
        {
            throw new ArgumentException("Instruction text must not be empty.", nameof(text));
        }
    }
}
