using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Throne.Application.Errors;

namespace Throne.Architecture.Tests;

public class ApiExceptionBoundaryRulesTests
{
    [Fact(DisplayName = "HTTP API не ловит ApiException в endpoint/controller коде")]
    public void Http_api_does_not_catch_ApiException_in_endpoint_code()
    {
        var offenders = new List<string>();
        using var module = ModuleDefinition.ReadModule(
            typeof(Throne.Api.AssemblyMarker).Assembly.Location
        );

        foreach (var type in module.GetTypes())
        {
            if (!IsHttpEndpointType(type))
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                if (method.Body.ExceptionHandlers.Any(IsApiExceptionCatch))
                {
                    offenders.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        offenders
            .Should()
            .BeEmpty(
                "HTTP-граница должна обрабатывать ApiException через глобальный IExceptionHandler. Offenders:{0}{1}",
                Environment.NewLine,
                string.Join(Environment.NewLine, offenders)
            );
    }

    private static bool IsApiExceptionCatch(ExceptionHandler handler) =>
        handler.HandlerType == ExceptionHandlerType.Catch
        && handler.CatchType?.FullName == typeof(ApiException).FullName;

    private static bool IsHttpEndpointType(TypeDefinition type) =>
        type.Name.EndsWith("Controller", StringComparison.Ordinal)
        || type.Name.Contains("Controller/", StringComparison.Ordinal)
        || type.Name.EndsWith("Endpoint", StringComparison.Ordinal)
        || type.Name.Contains("Endpoint/", StringComparison.Ordinal);
}
