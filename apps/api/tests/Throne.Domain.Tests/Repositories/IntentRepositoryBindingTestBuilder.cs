using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

internal static class IntentRepositoryBindingTestBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    public static IntentRepositoryBinding Pending(int? prNumber = null) =>
        IntentRepositoryBinding.Create(
            id: BindingId.New(),
            intentId: new IntentId("intent-abc"),
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, "anthropics", "throne"),
            defaultBranch: "main",
            workspacePath: "/tmp/throne/workspace/intent-abc/anthropics__throne",
            pullRequestNumber: prNumber,
            now: Now);

    public static IntentRepositoryBinding Ready(int? prNumber = null)
    {
        var binding = Pending(prNumber);
        binding.MarkCloning(Now.AddSeconds(1));
        binding.MarkReady(Now.AddSeconds(2));
        return binding;
    }
}
