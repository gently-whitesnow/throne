using Throne.Domain.Instructions;
using Throne.Instructions.Contracts.Generated;

namespace Throne.Api.Instructions;

internal static class InstructionDtoMapper
{
    private const int TextShortMaxLength = 140;

    public static InstructionListItemDto ToListDto(Instruction instruction) => new()
    {
        Id = instruction.Id.Value,
        Kind = instruction.Descriptor.Kind,
        Current_version = instruction.CurrentVersion,
        Text_short = TextShort(instruction.Text),
        Created_at = instruction.CreatedAt,
        Updated_at = instruction.UpdatedAt,
    };

    public static InstructionDetailDto ToDetailDto(Instruction instruction) => new()
    {
        Id = instruction.Id.Value,
        Kind = instruction.Descriptor.Kind,
        Current_version = instruction.CurrentVersion,
        Text = instruction.Text,
        Created_at = instruction.CreatedAt,
        Updated_at = instruction.UpdatedAt,
    };

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
