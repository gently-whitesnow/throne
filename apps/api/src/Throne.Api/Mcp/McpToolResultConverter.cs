using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp;

internal static class McpToolResultConverter
{
    public static CallToolResult ToCallToolResult(
        object? result,
        Tool protocolTool,
        JsonSerializerOptions serializerOptions)
    {
        var structuredContent = CreateStructuredContent(result, protocolTool, serializerOptions);

        return result switch
        {
            CallToolResult callToolResult => callToolResult,
            AIContent aiContent => new CallToolResult
            {
                Content = [aiContent.ToContentBlock(serializerOptions)],
                StructuredContent = structuredContent,
                IsError = aiContent is ErrorContent,
            },
            null => new CallToolResult
            {
                Content = [],
                StructuredContent = structuredContent,
                IsError = false,
            },
            string text => new CallToolResult
            {
                Content = [new TextContentBlock { Text = text }],
                StructuredContent = structuredContent,
                IsError = false,
            },
            ContentBlock content => new CallToolResult
            {
                Content = [content],
                StructuredContent = structuredContent,
                IsError = false,
            },
            IEnumerable<AIContent> aiContents => ConvertAIContents(aiContents, structuredContent, serializerOptions),
            IEnumerable<ContentBlock> contents => new CallToolResult
            {
                Content = contents.ToList(),
                StructuredContent = structuredContent,
                IsError = false,
            },
            _ => new CallToolResult
            {
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, serializerOptions.GetTypeInfo(typeof(object))) }],
                StructuredContent = structuredContent,
                IsError = false,
            },
        };
    }

    private static JsonElement? CreateStructuredContent(
        object? result,
        Tool protocolTool,
        JsonSerializerOptions serializerOptions)
    {
        if (protocolTool.OutputSchema is null || result is CallToolResult)
        {
            return null;
        }

        return result switch
        {
            JsonElement element => element,
            JsonNode node => JsonSerializer.SerializeToElement(node, serializerOptions.GetTypeInfo(typeof(JsonNode))),
            null => null,
            _ => JsonSerializer.SerializeToElement(result, serializerOptions.GetTypeInfo(typeof(object))),
        };
    }

    private static CallToolResult ConvertAIContents(
        IEnumerable<AIContent> contentItems,
        JsonElement? structuredContent,
        JsonSerializerOptions serializerOptions)
    {
        var content = new List<ContentBlock>();
        var allErrorContent = true;
        var hasAny = false;

        foreach (var item in contentItems)
        {
            content.Add(item.ToContentBlock(serializerOptions));
            hasAny = true;
            if (item is not ErrorContent)
            {
                allErrorContent = false;
            }
        }

        return new CallToolResult
        {
            Content = content,
            StructuredContent = structuredContent,
            IsError = allErrorContent && hasAny,
        };
    }

}
