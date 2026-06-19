using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalReadinessSignalsTests
{
    [Fact(DisplayName = "Signal завершает заранее зарегистрированный readiness latch")]
    public async Task Signal_completes_armed_latch()
    {
        var signals = new TerminalReadinessSignals();
        using var readiness = signals.Arm("intent-1");

        signals.TrySignal("intent-1").Should().BeTrue();

        await readiness.Ready.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "Release старой регистрации не удаляет новую регистрацию того же intent")]
    public async Task Old_registration_release_does_not_remove_new_registration()
    {
        var signals = new TerminalReadinessSignals();
        using var old = signals.Arm("intent-1");
        using var current = signals.Arm("intent-1");

        old.Dispose();
        signals.TrySignal("intent-1").Should().BeTrue();

        await current.Ready.WaitAsync(TimeSpan.FromSeconds(1));
        old.Ready.IsCompleted.Should().BeFalse();
    }
}
