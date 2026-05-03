namespace Throne.Domain.DreamRuns;

public readonly record struct DreamRunId(string Value)
{
    public static DreamRunId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
