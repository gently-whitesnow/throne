using Throne.Domain.PromptParts;

namespace Throne.Application.Ports;

public abstract record CreatePromptPartOutcome
{
    private CreatePromptPartOutcome() { }

    public sealed record Created(PromptPart Part) : CreatePromptPartOutcome;

    public sealed record KeyConflict : CreatePromptPartOutcome;
}

public abstract record ReplacePromptPartTextOutcome
{
    private ReplacePromptPartTextOutcome() { }

    public sealed record Replaced(PromptPart Part) : ReplacePromptPartTextOutcome;

    public sealed record NotFound : ReplacePromptPartTextOutcome;

    public sealed record VersionConflict(int CurrentVersion) : ReplacePromptPartTextOutcome;

    public sealed record MatchNotFound(string QueryPreview) : ReplacePromptPartTextOutcome;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplacePromptPartTextOutcome;
}

public abstract record DeletePromptPartOutcome
{
    private DeletePromptPartOutcome() { }

    public sealed record Deleted : DeletePromptPartOutcome;

    public sealed record NotFound : DeletePromptPartOutcome;

    public sealed record HasRoles : DeletePromptPartOutcome;
}
