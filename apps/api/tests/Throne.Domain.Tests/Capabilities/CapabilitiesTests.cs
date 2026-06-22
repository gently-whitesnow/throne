using FluentAssertions;
using Throne.Domain.Capabilities;

namespace Throne.Domain.Tests.Capabilities;

public class CapabilitiesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateEmpty стартует с пустыми selections и version=1")]
    public void CreateEmpty_starts_with_empty_state()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);

        aggregate.Selections.Should().BeEmpty();
        aggregate.CurrentVersion.Should().Be(1);
        aggregate.UpdatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "GetSelectedProvider по неперсиснутой capability возвращает null")]
    public void GetSelectedProvider_returns_null_for_unset_capability()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);

        aggregate.GetSelectedProvider(CapabilityNames.OpenInIde).Should().BeNull();
    }

    [Fact(DisplayName = "SetSelectedProvider на новое значение поднимает версию и обновляет updated_at")]
    public void SetSelectedProvider_changes_state_and_bumps_version()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);
        var later = Now.AddMinutes(5);

        var changed = aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "vscode", later);

        changed.Should().BeTrue();
        aggregate.GetSelectedProvider(CapabilityNames.OpenInIde).Should().Be("vscode");
        aggregate.CurrentVersion.Should().Be(2);
        aggregate.UpdatedAt.Should().Be(later);
    }

    [Fact(DisplayName = "Повторный SetSelectedProvider с тем же значением — no-op")]
    public void SetSelectedProvider_with_same_value_is_no_op()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);
        aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "vscode", Now.AddMinutes(1));
        var versionAfterFirst = aggregate.CurrentVersion;
        var updatedAfterFirst = aggregate.UpdatedAt;

        var changed = aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "vscode", Now.AddMinutes(10));

        changed.Should().BeFalse();
        aggregate.CurrentVersion.Should().Be(versionAfterFirst);
        aggregate.UpdatedAt.Should().Be(updatedAfterFirst);
    }

    [Fact(DisplayName = "SetSelectedProvider(null) очищает выбор")]
    public void SetSelectedProvider_null_clears()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);
        aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, "cursor", Now.AddMinutes(1));

        var changed = aggregate.SetSelectedProvider(CapabilityNames.OpenInIde, null, Now.AddMinutes(2));

        changed.Should().BeTrue();
        aggregate.GetSelectedProvider(CapabilityNames.OpenInIde).Should().BeNull();
    }

    [Fact(DisplayName = "SetSelectedProvider отвергает неизвестное имя capability")]
    public void SetSelectedProvider_rejects_unknown_capability_name()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.CreateEmpty(Now);

        var act = () => aggregate.SetSelectedProvider("plannotator", "x", Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore дропает legacy-ключи, не падая")]
    public void Restore_drops_legacy_keys_silently()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.Restore(
            currentVersion: 4,
            updatedAt: Now,
            selections: new Dictionary<string, string?>
            {
                ["vscode"] = "true",
                [CapabilityNames.OpenInIde] = "cursor",
            });

        aggregate.GetSelectedProvider(CapabilityNames.OpenInIde).Should().Be("cursor");
    }

    [Fact(DisplayName = "Restore возвращает агрегат с тем же selected_provider")]
    public void Restore_roundtrip()
    {
        var aggregate = Throne.Domain.Capabilities.Capabilities.Restore(
            7,
            Now,
            new Dictionary<string, string?> { [CapabilityNames.OpenInIde] = "vscode" });

        aggregate.CurrentVersion.Should().Be(7);
        aggregate.GetSelectedProvider(CapabilityNames.OpenInIde).Should().Be("vscode");
    }

    [Fact(DisplayName = "CapabilityNames.IsKnown содержит open_in_ide, не содержит legacy")]
    public void IsKnown_only_carrier_keys()
    {
        CapabilityNames.IsKnown(CapabilityNames.OpenInIde).Should().BeTrue();
        CapabilityNames.IsKnown("vscode").Should().BeFalse();
        CapabilityNames.IsKnown("gitlab").Should().BeFalse();
        CapabilityNames.IsKnown("nope").Should().BeFalse();
    }
}
