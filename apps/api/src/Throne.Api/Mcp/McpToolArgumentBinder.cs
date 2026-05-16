using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Throne.Api.Mcp;

/// <summary>
/// Конвертирует JSON-аргументы MCP-вызова в <see cref="AIFunctionArguments"/>,
/// обходя пробел в System.Text.Json schema generator: для nullable
/// collection/object параметров (<c>T? = null</c>) сгенерированная JSON-схема не
/// содержит <c>type</c>, и стандартный binder фейлится при попытке
/// десериализовать <c>JsonElement(Array)</c>. Здесь мы заранее десериализуем
/// каждый аргумент в реальный тип параметра — для всех tool'ов, не только
/// create_intent.tags.
/// </summary>
internal static class McpToolArgumentBinder
{
    public static AIFunctionArguments Build(
        AIFunction aiFunction,
        RequestContext<CallToolRequestParams> request)
    {
        var args = new AIFunctionArguments { Services = request.Services };
        if (request.Params?.Arguments is not { } argDict)
        {
            return args;
        }

        var parameters = McpToolParameterMap.For(aiFunction.UnderlyingMethod);
        foreach (var (key, value) in argDict)
        {
            args[key] = ConvertArgument(aiFunction.JsonSerializerOptions, value, key, parameters);
        }
        return args;
    }

    private static object? ConvertArgument(
        JsonSerializerOptions jsonOptions,
        JsonElement value,
        string key,
        IReadOnlyDictionary<string, Type>? parameters)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var targetType))
        {
            return value;
        }

        var effective = McpJsonArgumentUnwrapper.Unwrap(value, targetType);
        try
        {
            return JsonSerializer.Deserialize(effective, targetType, jsonOptions);
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
