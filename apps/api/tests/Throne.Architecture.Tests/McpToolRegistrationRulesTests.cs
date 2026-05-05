using FluentAssertions;
using Mono.Cecil;

namespace Throne.Architecture.Tests;

public class McpToolRegistrationRulesTests
{
    private static readonly string[] BannedSdkRegistrationMethods =
    [
        "WithTools",
        "WithToolsFromAssembly",
    ];

    private const string SdkBuilderExtensionsType =
        "Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions";

    private const string McpServerToolType = "ModelContextProtocol.Server.McpServerTool";

    private const string AllowedRegistrationType = "Throne.Api.Mcp.ThroneToolRegistration";

    [Fact(DisplayName = "Throne.Api не вызывает SDK WithTools/WithToolsFromAssembly — только AddThroneTool<T>")]
    public void Api_does_not_call_SDK_registration()
    {
        var offenders = new List<string>();
        ScanCalls(reference =>
            reference.DeclaringType.FullName == SdkBuilderExtensionsType &&
            BannedSdkRegistrationMethods.Contains(reference.Name),
            offenders);

        offenders.Should().BeEmpty(
            "Throne.Api must register MCP tools only via AddThroneTool<T>(). Offenders: {0}",
            string.Join(", ", offenders));
    }

    [Fact(DisplayName = "Throne.Api вызывает McpServerTool.Create только из ThroneToolRegistration (audit by construction)")]
    public void Only_registration_helper_calls_McpServerTool_Create()
    {
        var offenders = new List<string>();
        ScanCalls((reference, holder) =>
            reference.DeclaringType.FullName == McpServerToolType &&
            reference.Name == "Create" &&
            !holder.StartsWith(AllowedRegistrationType, StringComparison.Ordinal),
            offenders);

        offenders.Should().BeEmpty(
            "Only ThroneToolRegistration may call McpServerTool.Create — that is what guarantees AuditingMcpServerTool wrapping. Offenders: {0}",
            string.Join(", ", offenders));
    }

    private static void ScanCalls(Func<MethodReference, bool> predicate, List<string> offenders)
        => ScanCalls((reference, _) => predicate(reference), offenders);

    private static void ScanCalls(Func<MethodReference, string, bool> predicate, List<string> offenders)
    {
        var asmPath = typeof(Throne.Api.Mcp.ThroneToolRegistration).Assembly.Location;
        using var module = ModuleDefinition.ReadModule(asmPath);

        foreach (var type in EnumerateAllTypes(module))
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is MethodReference reference &&
                        predicate(reference, $"{type.FullName}.{method.Name}"))
                    {
                        offenders.Add($"{type.FullName}.{method.Name} -> {reference.DeclaringType.Name}.{reference.Name}");
                    }
                }
            }
        }
    }

    private static IEnumerable<TypeDefinition> EnumerateAllTypes(ModuleDefinition module)
    {
        foreach (var t in module.Types)
        {
            yield return t;
            foreach (var nested in t.NestedTypes)
            {
                yield return nested;
            }
        }
    }
}
