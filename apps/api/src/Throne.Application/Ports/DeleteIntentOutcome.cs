namespace Throne.Application.Ports;

public abstract record DeleteIntentOutcome
{
    private DeleteIntentOutcome() { }

    public sealed record Deleted : DeleteIntentOutcome;

    public sealed record NotFound : DeleteIntentOutcome;
}
