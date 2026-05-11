using System.Reflection;
using FluentAssertions;

namespace Throne.Architecture.Tests;

/// <summary>
/// MCP tools/prompts are invoked via Microsoft.Extensions.AI reflection (AIFunctionFactory).
/// Nullable annotations (<c>string?</c>, <c>int?</c>) do <b>not</b> make a parameter omittable on the wire:
/// missing JSON keys still throw unless the C# parameter has an explicit default value.
/// </summary>
public sealed class McpBoundMethodParameterRulesTests
{
    private const string ToolHostAttribute = "McpServerToolTypeAttribute";
    private const string PromptHostAttribute = "McpServerPromptTypeAttribute";
    private const string ToolMethodAttribute = "McpServerToolAttribute";
    private const string PromptMethodAttribute = "McpServerPromptAttribute";

    [Fact(DisplayName = "MCP tool/prompt: nullable параметр ⇒ default в сигнатуре (иначе AIFunction требует ключ в JSON)")]
    public void Nullable_parameters_on_mcp_bound_methods_must_have_cli_defaults()
    {
        var assembly = typeof(Throne.Api.AssemblyMarker).Assembly;
        var ctx = new NullabilityInfoContext();
        var violations = new List<string>();

        foreach (var type in SafeExportedTypes(assembly))
        {
            if (!TypeHasAttributeNamed(type, ToolHostAttribute) &&
                !TypeHasAttributeNamed(type, PromptHostAttribute))
            {
                continue;
            }

            const BindingFlags McpMethodFlags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var method in type.GetMethods(McpMethodFlags))
            {
                if (!MethodHasAttributeNamed(method, ToolMethodAttribute) &&
                    !MethodHasAttributeNamed(method, PromptMethodAttribute))
                {
                    continue;
                }

                foreach (var param in method.GetParameters())
                {
                    if (param.ParameterType == typeof(CancellationToken))
                    {
                        continue;
                    }

                    var info = ctx.Create(param);
                    if (info.ReadState != NullabilityState.Nullable)
                    {
                        continue;
                    }

                    if (!param.HasDefaultValue)
                    {
                        violations.Add(
                            $"{type.FullName}.{method.Name}({DescribeParameter(param)}): nullable without default — " +
                            "MCP clients omitting the key get ArgumentException at runtime.");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            "Nullable-параметр без default — это runtime ловушка: AIFunctionFactory требует ключ в JSON " +
            "и бросит ArgumentException, если клиент не передал поле. " +
            "Чини добавлением \"= null\" или \"= default\" в сигнатуре MCP-метода. Violations:{0}{1}",
            Environment.NewLine,
            string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<Type> SafeExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (TypeLoadException)
        {
            return [];
        }
    }

    private static bool TypeHasAttributeNamed(MemberInfo member, string attributeShortName) =>
        member.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == attributeShortName);

    private static bool MethodHasAttributeNamed(MethodInfo method, string attributeShortName) =>
        method.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == attributeShortName);

    private static string DescribeParameter(ParameterInfo p) =>
        $"{p.ParameterType.Name} {p.Name}";
}
