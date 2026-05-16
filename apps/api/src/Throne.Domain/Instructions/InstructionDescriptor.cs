namespace Throne.Domain.Instructions;

public sealed record InstructionDescriptor(string Scope, string? UserId, string Kind);
