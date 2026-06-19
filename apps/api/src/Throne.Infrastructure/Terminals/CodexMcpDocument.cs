using Throne.Application.Terminals;
using Tomlyn;
using Tomlyn.Syntax;

namespace Throne.Infrastructure.Terminals;

internal static class CodexMcpDocument
{
    private const string McpServersKey = "mcp_servers";
    private const string UrlKey = "url";
    private const string CommandKey = "command";
    private const string ArgsKey = "args";
    private const string EnvKey = "env";

    public static string? WithThroneServer(string? existingToml, string? apiBaseUrl)
    {
        var baseText = existingToml ?? string.Empty;
        var doc = Toml.Parse(baseText);
        if (doc.HasErrors)
        {
            return null;
        }

        var url = ThroneMcpServerConfig.Url(apiBaseUrl);
        var table = FindThroneTable(doc);
        if (table is not null)
        {
            var current = FindValue(table, UrlKey);
            if (IsStringValue(current, url) && HasNoStdioKeys(table))
            {
                return null;
            }

            RemoveStdioKeys(table);
            SetUrl(table, current, url);
            return doc.ToString();
        }

        if (HasTopLevelMcpServersKey(doc))
        {
            return null;
        }

        return Append(baseText, url);
    }

    private static TableSyntax? FindThroneTable(DocumentSyntax doc)
    {
        foreach (var table in doc.Tables)
        {
            if (table is TableSyntax t && IsThroneTable(t))
            {
                return t;
            }
        }

        return null;
    }

    private static bool IsThroneTable(TableSyntax table)
    {
        var name = table.Name?.ToString().Trim();
        return name == $"{McpServersKey}.{ThroneMcpServerConfig.Name}"
            || name == $"{McpServersKey}.\"{ThroneMcpServerConfig.Name}\""
            || name == $"{McpServersKey}.'{ThroneMcpServerConfig.Name}'";
    }

    private static KeyValueSyntax? FindValue(TableSyntax table, string key)
    {
        foreach (var item in table.Items)
        {
            if (item.Key?.ToString().Trim() == key)
            {
                return item;
            }
        }

        return null;
    }

    private static bool IsStringValue(KeyValueSyntax? item, string expected) =>
        item?.Value is StringValueSyntax value && value.Value == expected;

    private static bool HasNoStdioKeys(TableSyntax table) =>
        FindValue(table, CommandKey) is null
        && FindValue(table, ArgsKey) is null
        && FindValue(table, EnvKey) is null;

    private static void RemoveStdioKeys(TableSyntax table)
    {
        RemoveValue(table, CommandKey);
        RemoveValue(table, ArgsKey);
        RemoveValue(table, EnvKey);
    }

    private static void RemoveValue(TableSyntax table, string key)
    {
        var item = FindValue(table, key);
        if (item is not null)
        {
            table.Items.RemoveChild(item);
        }
    }

    private static void SetUrl(TableSyntax table, KeyValueSyntax? current, string url)
    {
        if (current is not null)
        {
            current.Value = new StringValueSyntax(url);
            return;
        }

        table.Items.Add(new KeyValueSyntax(UrlKey, new StringValueSyntax(url)));
    }

    private static bool HasTopLevelMcpServersKey(DocumentSyntax doc)
    {
        foreach (var kv in doc.KeyValues)
        {
            if (kv.Key?.ToString().Trim() == McpServersKey)
            {
                return true;
            }
        }

        return false;
    }

    private static string Append(string baseText, string url)
    {
        var block = $"[{McpServersKey}.{ThroneMcpServerConfig.Name}]\n{UrlKey} = {CodexConfigValue.ToToml(url)}\n";
        if (baseText.Length == 0)
        {
            return block;
        }

        var newline = baseText.EndsWith('\n') ? string.Empty : "\n";
        return baseText + newline + "\n" + block;
    }
}
