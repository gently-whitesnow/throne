namespace Throne.Domain.Instructions;

internal static class InstructionScopeRules
{
    public static void EnsureUserIdMatchesScope(string scope, string? userId)
    {
        if (scope == InstructionScopeNames.User)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("user_id is required for user-scoped instructions.", nameof(userId));
            }
        }
        else if (userId is not null)
        {
            throw new ArgumentException("user_id must be null for system-scoped instructions.", nameof(userId));
        }
    }
}
