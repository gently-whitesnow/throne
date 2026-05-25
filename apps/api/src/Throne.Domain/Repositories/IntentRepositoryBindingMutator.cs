namespace Throne.Domain.Repositories;

/// <summary>
/// Clone-status transition operations for <see cref="IntentRepositoryBinding"/>. Pull-request
/// operations live in <see cref="IntentRepositoryBindingPullRequestMutator"/>.
/// </summary>
public static class IntentRepositoryBindingMutator
{
    public static void MarkCloning(this IntentRepositoryBinding binding, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        IntentRepositoryBindingGuards.EnsureTransition(
            binding.State.CloneStatus, CloneStatusNames.Pending, CloneStatusNames.Cloning);
        binding.State = binding.State with
        {
            CloneStatus = CloneStatusNames.Cloning,
            CloneError = null,
            UpdatedAt = at,
        };
    }

    public static void MarkReady(this IntentRepositoryBinding binding, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        IntentRepositoryBindingGuards.EnsureTransition(
            binding.State.CloneStatus, CloneStatusNames.Cloning, CloneStatusNames.Ready);
        binding.State = binding.State with
        {
            CloneStatus = CloneStatusNames.Ready,
            CloneError = null,
            UpdatedAt = at,
        };
    }

    public static void MarkFailed(this IntentRepositoryBinding binding, string error, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        IntentRepositoryBindingGuards.EnsureFailedTransition(binding.State.CloneStatus);
        binding.State = binding.State with
        {
            CloneStatus = CloneStatusNames.Failed,
            CloneError = error,
            UpdatedAt = at,
        };
    }

    public static void MarkBroken(this IntentRepositoryBinding binding, string reason, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        IntentRepositoryBindingGuards.EnsureTransition(
            binding.State.CloneStatus, CloneStatusNames.Ready, CloneStatusNames.Broken);
        binding.State = binding.State with
        {
            CloneStatus = CloneStatusNames.Broken,
            CloneError = reason,
            UpdatedAt = at,
        };
    }
}
