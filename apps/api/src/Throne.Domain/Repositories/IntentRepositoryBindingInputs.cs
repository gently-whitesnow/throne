namespace Throne.Domain.Repositories;

/// <summary>
/// Common input guards shared by <see cref="IntentRepositoryBindingFactory"/>
/// (used by both Create and Restore). Restore-only enum/PR validations live in
/// <see cref="IntentRepositoryBindingRestoreValidator"/>.
/// </summary>
internal static class IntentRepositoryBindingInputs
{
    public static void EnsureCommonInputs(RepoCoordinate coordinate, string defaultBranch, string workspacePath)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
    }

    public static void EnsurePositivePullRequestNumber(int? number)
    {
        if (number is { } value && value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Pull request number must be >= 1.");
        }
    }
}
