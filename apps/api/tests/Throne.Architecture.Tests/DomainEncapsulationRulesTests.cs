using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Throne.Architecture.Tests;

/// <summary>
/// ADR-0025: Throne.Domain нормативно ведётся в rich-DDD стиле — state-переходы
/// инстансными методами агрегата, инкапсуляция через <c>{ get; private set; }</c>
/// или иммутабельный record-state с приватной заменой. Свойств с
/// <c>internal</c>-сеттером быть не должно: они подсказывают persistence /
/// Application мутировать состояние в обход агрегата (исторически —
/// <c>Intent.State { get; internal set; }</c>, мутируемое из соседних static-классов
/// <c>*Operation</c> / <c>*Mutator</c>).
/// </summary>
public class DomainEncapsulationRulesTests
{
    private static readonly Assembly DomainAsm =
        typeof(Throne.Domain.AssemblyMarker).Assembly;

    [Fact(DisplayName = "ADR-0025: типы в Throne.Domain не должны иметь свойств с internal-сеттером")]
    public void DomainTypes_should_not_have_internal_setters()
    {
        var violations = DomainAsm.GetTypes()
            .Where(t => !IsCompilerGenerated(t))
            .SelectMany(t => t.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsAssembly: true })
                .Select(p => $"{t.FullName}.{p.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "ADR-0025 запрещает internal-сеттеры в Throne.Domain; " +
            "state должен меняться через инстансные методы агрегата.");
    }

    private static bool IsCompilerGenerated(Type t) =>
        t.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
        || (t.DeclaringType is { } parent && IsCompilerGenerated(parent));
}
