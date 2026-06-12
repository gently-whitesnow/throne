using Throne.Application.PromptParts;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

internal static class TerminalPreviewMapper
{
    public static IntentTerminalPreviewResponse ToDto(string intentId, TerminalRunMode mode, PromptComposition composition)
    {
        var parts = new List<PromptPartPreviewDto>(composition.Parts.Count);
        foreach (var part in composition.Parts)
        {
            parts.Add(new PromptPartPreviewDto
            {
                Part_id = part.PartId,
                Key = part.Key,
                Scope = part.Scope,
                Role = part.Role,
                Order = part.Order,
                Editable = part.Editable,
                Present = part.Present,
                Selected = part.Selected,
                Text = part.Text,
            });
        }

        return new IntentTerminalPreviewResponse
        {
            Intent_id = intentId,
            Mode = mode,
            Parts = parts,
            Selected_part_ids = composition.SelectedPartIds.ToList(),
            System_prompt = composition.SystemPrompt,
            User_prompt = composition.UserPrompt,
        };
    }
}
