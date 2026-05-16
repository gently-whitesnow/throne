namespace Throne.Domain.Instructions;

public static class InstructionFactory
{
    public static Instruction Create(
        InstructionId id,
        string scope,
        string? userId,
        string kind,
        string text,
        DateTimeOffset now)
    {
        InstructionGuards.EnsureCreateInputs(scope, userId, kind, text);
        return new Instruction(id, new InstructionDescriptor(scope, userId, kind), text, currentVersion: 1, now, now);
    }

    public static Instruction Restore(
        InstructionId id,
        string scope,
        string? userId,
        string kind,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        InstructionGuards.EnsureCreateInputs(scope, userId, kind, text);
        InstructionGuards.EnsureValidCurrentVersion(currentVersion);
        return new Instruction(id, new InstructionDescriptor(scope, userId, kind), text, currentVersion, createdAt, updatedAt);
    }
}
