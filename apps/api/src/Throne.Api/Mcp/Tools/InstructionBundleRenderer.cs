using System.Text;
using System.Text.Json.Nodes;
using Throne.Application.Instructions;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Готовит ответ `get_instruction_bundle` без дублирования полезной нагрузки.
///
/// MCP-сервер SDK 1.2.0 при отдаче <see cref="ModelContextProtocol.Protocol.CallToolResult"/> по wire
/// прогоняет каждый строковый филд через дефолтный <c>JavaScriptEncoder</c>, который экранирует non-ASCII
/// как <c>\uXXXX</c>. Если положить полный JSON-сериализованный bundle и в <c>TextContentBlock.Text</c>,
/// и в <c>StructuredContent</c> (поведение SDK-овской авто-конверсии при <c>UseStructuredContent = true</c>),
/// текст инструкций уезжает по сети дважды и каждый кириллический символ занимает 6 байт. На ~8 КБ полезного
/// текста ответ доходил до 70 КБ — клиент-харнес отказывался читать tool result.
///
/// Согласно обсуждению в csharp-sdk#626 / #962 (PederHP, eiriktsarpalis) идиоматичное решение для tool-ов,
/// чей потребитель — модель: рендерить читабельный текст в <c>Content</c>, а в <c>StructuredContent</c> класть
/// только метаданные. <c>Throne.Api.Mcp.McpResultSummarizer</c> читает из StructuredContent только refs
/// (scope/kind/instruction_id/current_version) и <c>missing_kinds</c> — их и оставляем.
/// </summary>
internal static class InstructionBundleRenderer
{
    public static string RenderText(InstructionBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var sb = new StringBuilder(EstimateCapacity(bundle));
        sb.Append("mode=").Append(bundle.Mode);
        if (!string.IsNullOrEmpty(bundle.IntentId))
        {
            sb.Append(" intent_id=").Append(bundle.IntentId);
        }
        sb.Append('\n');

        foreach (var instruction in bundle.Instructions)
        {
            sb.Append("\n===== ")
              .Append(instruction.Scope).Append(':').Append(instruction.Kind)
              .Append(" (v").Append(instruction.CurrentVersion)
              .Append(", id=").Append(instruction.InstructionId)
              .Append(") =====\n\n");
            sb.Append(instruction.Text);
            if (!instruction.Text.EndsWith('\n'))
            {
                sb.Append('\n');
            }
        }

        if (bundle.MissingKinds.Count > 0)
        {
            sb.Append("\n[missing_kinds: ")
              .Append(string.Join(", ", bundle.MissingKinds))
              .Append("]\n");
        }

        return sb.ToString();
    }

    public static JsonObject RenderStructured(InstructionBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var refs = new JsonArray();
        foreach (var instruction in bundle.Instructions)
        {
            refs.Add(new JsonObject
            {
                ["scope"] = instruction.Scope,
                ["kind"] = instruction.Kind,
                ["instruction_id"] = instruction.InstructionId,
                ["current_version"] = instruction.CurrentVersion,
            });
        }

        var missing = new JsonArray();
        foreach (var kind in bundle.MissingKinds)
        {
            missing.Add(kind);
        }

        return new JsonObject
        {
            ["mode"] = bundle.Mode,
            ["intent_id"] = bundle.IntentId,
            ["instructions"] = refs,
            ["missing_kinds"] = missing,
        };
    }

    private static int EstimateCapacity(InstructionBundle bundle)
    {
        var total = 64;
        foreach (var instruction in bundle.Instructions)
        {
            total += 80 + instruction.Text.Length;
        }
        return total;
    }
}
