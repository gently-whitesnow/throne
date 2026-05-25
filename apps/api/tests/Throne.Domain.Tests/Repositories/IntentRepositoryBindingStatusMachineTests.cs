using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class IntentRepositoryBindingStatusMachineTests
{
    private static readonly DateTimeOffset Now = IntentRepositoryBindingTestBuilder.Now;

    [Fact(DisplayName = "Happy path: pending → cloning → ready очищает clone_error")]
    public void Pending_to_cloning_to_ready()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();

        binding.MarkCloning(Now.AddSeconds(1));
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Cloning);
        binding.State.UpdatedAt.Should().Be(Now.AddSeconds(1));

        binding.MarkReady(Now.AddSeconds(2));
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Ready);
        binding.State.CloneError.Should().BeNull();
        binding.State.UpdatedAt.Should().Be(Now.AddSeconds(2));
    }

    [Fact(DisplayName = "MarkFailed разрешён из cloning с непустым error")]
    public void Cloning_to_failed_records_error()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();
        binding.MarkCloning(Now.AddSeconds(1));

        binding.MarkFailed("authentication failed", Now.AddSeconds(2));

        binding.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
        binding.State.CloneError.Should().Be("authentication failed");
    }

    [Fact(DisplayName = "MarkFailed разрешён из pending (restart-interrupted)")]
    public void Pending_to_failed_allowed_on_restart()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();

        binding.MarkFailed("interrupted", Now.AddSeconds(1));

        binding.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
    }

    [Theory(DisplayName = "MarkFailed запрещён из ready/failed/broken")]
    [InlineData(CloneStatusNames.Ready)]
    [InlineData(CloneStatusNames.Failed)]
    [InlineData(CloneStatusNames.Broken)]
    public void Failed_forbidden_from_terminal_or_ready(string fromStatus)
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();
        DriveTo(binding, fromStatus);

        var act = () => binding.MarkFailed("nope", Now.AddSeconds(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "MarkReady требует cloning")]
    public void Ready_requires_cloning()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();

        var act = () => binding.MarkReady(Now.AddSeconds(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "MarkBroken разрешён только из ready, и записывает reason в clone_error")]
    public void Broken_only_from_ready()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready();

        binding.MarkBroken("404 upstream", Now.AddSeconds(10));

        binding.State.CloneStatus.Should().Be(CloneStatusNames.Broken);
        binding.State.CloneError.Should().Be("404 upstream");
    }

    [Theory(DisplayName = "MarkBroken запрещён из не-ready: pending, cloning, failed, broken")]
    [InlineData(CloneStatusNames.Pending)]
    [InlineData(CloneStatusNames.Cloning)]
    [InlineData(CloneStatusNames.Failed)]
    [InlineData(CloneStatusNames.Broken)]
    public void Broken_forbidden_from_non_ready(string fromStatus)
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();
        DriveTo(binding, fromStatus);

        var act = () => binding.MarkBroken("404", Now.AddSeconds(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "MarkBroken требует непустой reason")]
    public void Broken_requires_reason()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready();

        var act = () => binding.MarkBroken("   ", Now.AddSeconds(10));

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "MarkFailed требует непустой error")]
    public void Failed_requires_error()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();
        binding.MarkCloning(Now.AddSeconds(1));

        var act = () => binding.MarkFailed("", Now.AddSeconds(2));

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "WorkspacePath неизменяем — нет публичного сеттера")]
    public void WorkspacePath_is_immutable()
    {
        var prop = typeof(IntentRepositoryBinding).GetProperty(nameof(IntentRepositoryBinding.WorkspacePath));

        prop.Should().NotBeNull();
        prop!.CanWrite.Should().BeFalse("workspace_path неизменяем после создания (ADR-0024)");
    }

    private static void DriveTo(IntentRepositoryBinding binding, string targetStatus)
    {
        switch (targetStatus)
        {
            case CloneStatusNames.Pending:
                return;
            case CloneStatusNames.Cloning:
                binding.MarkCloning(Now.AddSeconds(1));
                return;
            case CloneStatusNames.Ready:
                binding.MarkCloning(Now.AddSeconds(1));
                binding.MarkReady(Now.AddSeconds(2));
                return;
            case CloneStatusNames.Failed:
                binding.MarkCloning(Now.AddSeconds(1));
                binding.MarkFailed("boom", Now.AddSeconds(2));
                return;
            case CloneStatusNames.Broken:
                binding.MarkCloning(Now.AddSeconds(1));
                binding.MarkReady(Now.AddSeconds(2));
                binding.MarkBroken("404", Now.AddSeconds(3));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "unknown");
        }
    }
}
