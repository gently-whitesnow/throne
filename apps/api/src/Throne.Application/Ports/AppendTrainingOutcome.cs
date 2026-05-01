namespace Throne.Application.Ports;

public abstract record AppendTrainingOutcome
{
    private AppendTrainingOutcome() { }

    public sealed record Appended(int CurrentVersion) : AppendTrainingOutcome;

    public sealed record NotFound : AppendTrainingOutcome;

    public sealed record VersionConflict(int CurrentVersion) : AppendTrainingOutcome;
}
