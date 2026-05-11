using Throne.Application.Instructions;
using Throne.Instructions.Contracts.Generated;

namespace Throne.Api.Instructions;

internal static class BundlesTreeDtoMapper
{
    public static BundlesTreeDto ToDto(BundlesTree tree)
    {
        var dto = new BundlesTreeDto();
        foreach (var bundle in tree.Bundles)
        {
            var bundleDto = new BundleNodeDto { Mode = bundle.Mode };
            foreach (var entry in bundle.Includes)
            {
                bundleDto.Includes.Add(new BundleEntryNodeDto
                {
                    Scope = entry.Scope,
                    Kind = entry.Kind,
                    Instruction_id = entry.InstructionId,
                    Current_version = entry.CurrentVersion,
                    Text = entry.Text,
                    Editable = entry.Editable,
                    Present = entry.Present,
                });
            }
            dto.Bundles.Add(bundleDto);
        }
        return dto;
    }
}
