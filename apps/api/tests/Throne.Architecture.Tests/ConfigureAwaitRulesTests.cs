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
        typeof(Throne.Domain.Intents.Intent).Assembly.Location,
        typeof(Throne.Application.Intents.IntentAttachment).Assembly.Location,
        typeof(Throne.Infrastructure.DependencyInjection).Assembly.Location,
        typeof(Throne.Api.Mcp.Tools.IntentTools).Assembly.Location,
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
            "ConfigureAwait не нужен в .NET-сервисном коде (нет SynchronizationContext).");
    }
}
