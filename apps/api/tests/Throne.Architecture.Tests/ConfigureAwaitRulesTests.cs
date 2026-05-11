using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Throne.Architecture.Tests;

/// <summary>
/// В .NET-сервисном коде Throne нет SynchronizationContext, поэтому ConfigureAwait —
/// шум. Запрещаем его в продакшен-сборках, чтобы фикс не отменялся повторными добавлениями.
/// </summary>
public class ConfigureAwaitRulesTests
{
    private static readonly string[] ProductionAssemblies =
    [
        typeof(Throne.Domain.AssemblyMarker).Assembly.Location,
        typeof(Throne.Application.AssemblyMarker).Assembly.Location,
        typeof(Throne.Infrastructure.AssemblyMarker).Assembly.Location,
        typeof(Throne.Api.AssemblyMarker).Assembly.Location,
    ];

    [Fact(DisplayName = "В production-ассемблях нет вызовов Task.ConfigureAwait")]
    public void Production_assemblies_do_not_call_ConfigureAwait()
    {
        var offenders = new List<string>();

        foreach (var path in ProductionAssemblies)
        {
            using var module = ModuleDefinition.ReadModule(path);
            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody)
                    {
                        continue;
                    }

                    foreach (var instr in method.Body.Instructions)
                    {
                        if (instr.Operand is MethodReference mr
                            && mr.Name == "ConfigureAwait"
                            && (mr.DeclaringType.FullName.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal)
                                || mr.DeclaringType.FullName.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal)))
                        {
                            offenders.Add($"{type.FullName}.{method.Name}");
                            break;
                        }
                    }
                }
            }
        }

        offenders.Should().BeEmpty(
            "ConfigureAwait не нужен в Throne (server-side, нет SynchronizationContext) и создаёт визуальный шум. " +
            "Удали .ConfigureAwait(...) — поведение не изменится. Offenders:{0}{1}",
            Environment.NewLine,
            string.Join(Environment.NewLine, offenders));
    }
}
