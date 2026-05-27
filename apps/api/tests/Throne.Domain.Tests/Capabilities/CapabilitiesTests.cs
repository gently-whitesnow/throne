using FluentAssertions;
using Throne.Domain.Capabilities;

namespace Throne.Domain.Tests.Capabilities;

public class CapabilitiesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateEmpty стартует с пустыми toggles и version=1")]
    public void CreateEmpty_starts_with_empty_state()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);

        aggregate.Toggles.Should().BeEmpty();
        aggregate.CurrentVersion.Should().Be(1);
        aggregate.UpdatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "IsEnabled по неперсиснутой capability возвращает false")]
    public void IsEnabled_returns_false_for_unpersisted_capability()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);

        aggregate.IsEnabled(CapabilityNames.Terminal).Should().BeFalse();
    }

    [Fact(DisplayName = "SetEnabled на новое значение поднимает версию и обновляет updated_at")]
    public void SetEnabled_changes_state_and_bumps_version()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);
        var later = Now.AddMinutes(5);

        var changed = aggregate.SetEnabled(CapabilityNames.Terminal, enabled: true, later);

        changed.Should().BeTrue();
        aggregate.IsEnabled(CapabilityNames.Terminal).Should().BeTrue();
        aggregate.CurrentVersion.Should().Be(2);
        aggregate.UpdatedAt.Should().Be(later);
    }

    [Fact(DisplayName = "Повторный SetEnabled с тем же значением — no-op")]
    public void SetEnabled_with_same_value_is_no_op()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);
        aggregate.SetEnabled(CapabilityNames.Vscode, true, Now.AddMinutes(1));
        var versionAfterFirst = aggregate.CurrentVersion;
        var updatedAfterFirst = aggregate.UpdatedAt;

        var changed = aggregate.SetEnabled(CapabilityNames.Vscode, true, Now.AddMinutes(10));

        changed.Should().BeFalse();
        aggregate.CurrentVersion.Should().Be(versionAfterFirst);
        aggregate.UpdatedAt.Should().Be(updatedAfterFirst);
    }

    [Fact(DisplayName = "SetEnabled отвергает неизвестное имя capability")]
    public void SetEnabled_rejects_unknown_capability_name()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);

        var act = () => aggregate.SetEnabled("plannotator", true, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore проверяет каждое имя и роняет на неизвестных ключах")]
    public void Restore_rejects_unknown_keys_in_storage()
    {
        var act = () => Throne.Domain.Capabilities.Capabilities.Restore(
            currentVersion: 4,
            updatedAt: Now,
            toggles: new Dictionary<string, bool> { ["bogus"] = true });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore возвращает агрегат с теми же toggle'ами")]
    public void Restore_roundtrip()
    {
        var toggles = new Dictionary<string, bool>
        {
            [CapabilityNames.Repositories] = true,
            [CapabilityNames.Terminal] = false,
        };

        var aggregate = Throne.Domain.Capabilities.Capabilities.Restore(7, Now, toggles);

        aggregate.CurrentVersion.Should().Be(7);
        aggregate.IsEnabled(CapabilityNames.Repositories).Should().BeTrue();
        aggregate.IsEnabled(CapabilityNames.Terminal).Should().BeFalse();
    }

    [Fact(DisplayName = "CapabilityNames.IsKnown принимает зарезервированные jira/gitlab")]
    public void IsKnown_accepts_reserved_keys()
    {
        CapabilityNames.IsKnown(CapabilityNames.Jira).Should().BeTrue();
        CapabilityNames.IsKnown(CapabilityNames.Gitlab).Should().BeTrue();
        CapabilityNames.IsKnown("nope").Should().BeFalse();
    }
}
