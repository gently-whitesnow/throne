namespace Throne.Domain.DreamRuns;

public readonly record struct DreamProposalId(string Value)
{
    public static DreamProposalId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
