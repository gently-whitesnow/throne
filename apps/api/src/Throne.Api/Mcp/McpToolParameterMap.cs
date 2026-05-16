using System.Collections.Concurrent;
using System.Reflection;

namespace Throne.Api.Mcp;

internal static class McpToolParameterMap
{
    private static readonly ConcurrentDictionary<MethodInfo, IReadOnlyDictionary<string, Type>> Cache = new();

    public static IReadOnlyDictionary<string, Type>? For(MethodInfo? method)
    {
        if (method is null)
        {
            return null;
        }
        return Cache.GetOrAdd(method, BuildMap);
    }

    private static IReadOnlyDictionary<string, Type> BuildMap(MethodInfo m)
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var p in m.GetParameters())
        {
            if (p.Name is null || p.ParameterType == typeof(CancellationToken))
            {
                continue;
            }
            map[p.Name] = p.ParameterType;
        }
        return map;
    }
}
