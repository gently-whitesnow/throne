using System.Text;
using System.Text.Json;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal static class OpencodePluginShim
{
    public const string PluginDirectory = ".opencode/plugins";
    public const string PluginFileName = "throne.js";
    public const string MinimumSupportedCliVersion = "1.17.7";

    public static async Task<string> WriteAsync(
        string workspacePath, string intentId, string mode, string apiBaseUrl, CancellationToken ct)
    {
        var pluginDirectory = Path.Combine(workspacePath, ".opencode", "plugins");
        Directory.CreateDirectory(pluginDirectory);

        var path = Path.Combine(pluginDirectory, PluginFileName);
        await File.WriteAllTextAsync(path, Build(intentId, mode, apiBaseUrl), Encoding.UTF8, ct);
        return path;
    }

    public static string Build(string intentId, string mode, string apiBaseUrl)
    {
        var eventBindings = TerminalHookEvents.OpenCodeBindings
            .Where(binding => binding.BindingType == TerminalHookEvents.OpenCodeBindingEvent)
            .Select(binding => $"  [{Js(binding.OpenCodeHook)}, {Js(binding.ThroneEvent)}],");
        var typedBindings = TerminalHookEvents.OpenCodeBindings
            .Where(binding => binding.BindingType == TerminalHookEvents.OpenCodeBindingTypedHook)
            .Select(binding => $"    {Js(binding.OpenCodeHook)}: async () => post({Js(binding.ThroneEvent)}),");

        return string.Join('\n',
            "const apiBaseUrl = " + Js(apiBaseUrl.TrimEnd('/')) + ";",
            "const intentId = " + Js(intentId) + ";",
            "const mode = " + Js(mode) + ";",
            "const eventBindings = new Map([",
            string.Join('\n', eventBindings),
            "]);",
            "",
            "export const ThroneLifecyclePlugin = async ({ $ }) => {",
            "  const post = async (hookEvent) => {",
            "    const path = `/api/v1/intents/${encodeURIComponent(intentId)}` +",
            "      `/terminal/hooks/${encodeURIComponent(hookEvent)}?mode=${encodeURIComponent(mode)}`;",
            "    try {",
            "      await $`curl -s -X POST ${apiBaseUrl + path}`;",
            "    } catch (error) {",
            "      console.error('[throne] terminal hook failed', hookEvent, error);",
            "    }",
            "  };",
            "",
            "  return {",
            "    event: async ({ event }) => {",
            "      const hookEvent = eventBindings.get(event?.type);",
            "      if (hookEvent) await post(hookEvent);",
            "    },",
            string.Join('\n', typedBindings),
            "  };",
            "};",
            "");
    }

    private static string Js(string value) => JsonSerializer.Serialize(value);
}
