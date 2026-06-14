using FluentAssertions;
using NetArchTest.Rules;

namespace Throne.Architecture.Tests;

/// <summary>
/// ADR-0025: агрегаты Throne.Domain ведутся в rich-DDD стиле — фабрика, инварианты
/// и переходы живут на самом агрегате, а не в соседних static-сателлитах с суффиксами
/// <c>*Factory</c> / <c>*Guards</c> / <c>*Mutator</c> / <c>*Operation</c>. После миграции
/// 6 агрегатов таких типов в Domain не осталось; этот тест ловит регресс — новый
/// satellite-файл с одним из суффиксов валит сборку, а не только code review.
///
/// Исключения — value-object'ы и computational helpers, которые ADR-0025 оставляет
/// как есть (Out of scope). На текущий момент это единственный <c>RepoCoordinateGuards</c>
/// (валидатор VO <c>RepoCoordinate</c>). Расширять allow-list — осознанное решение:
/// добавляй сюда вместе с обоснованием в коммите.
/// </summary>
public class DomainAggregateSuffixRulesTests
{
    private static readonly System.Reflection.Assembly DomainAsm =
        typeof(Throne.Domain.AssemblyMarker).Assembly;

    private static readonly string[] ForbiddenSuffixes =
        ["Factory", "Guards", "Mutator", "Operation"];

    // VO/helper-валидаторы, которые ADR-0025 явно оставляет вне rich-DDD-миграции.
    private static readonly string[] AllowedTypeNames =
        ["RepoCoordinateGuards"];

    [Fact(DisplayName = "ADR-0025: в Throne.Domain нет satellite-типов *Factory/*Guards/*Mutator/*Operation")]
    public void DomainTypes_should_not_use_aggregate_satellite_suffixes()
    {
        var violations = Types
            .InAssembly(DomainAsm)
            .That()
            .ResideInNamespaceStartingWith("Throne.Domain")
            .GetTypes()
            .Where(t => !t.Name.Contains('<', StringComparison.Ordinal))
            .Where(t => !AllowedTypeNames.Contains(t.Name))
            .Where(t => ForbiddenSuffixes.Any(s => t.Name.EndsWith(s, StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "ADR-0025 запрещает выносить фабрику/инварианты/переходы агрегата в отдельные " +
            "*Factory/*Guards/*Mutator/*Operation. Перенеси их на сам агрегат (static Create/Restore, " +
            "инстансные методы перехода). Если это VO/helper-валидатор вне rich-DDD — добавь имя в " +
            "AllowedTypeNames с обоснованием. Нарушители: {0}",
            string.Join(", ", violations));
    }
}
