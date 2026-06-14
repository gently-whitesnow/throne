using Throne.Application.PromptParts;
using Throne.PromptParts.Contracts.Generated;

namespace Throne.Api.PromptParts;

internal static class BundlesTreeDtoMapper
{
    public static BundlesTreeDto ToDto(BundlesTree tree)
    {
        var dto = new BundlesTreeDto();
        foreach (var bundle in tree.Bundles)
        {
            var bundleDto = new BundleNodeDto { Mode = bundle.Mode, Contour = bundle.Contour };
            foreach (var entry in bundle.Includes)
            {
                bundleDto.Includes.Add(new BundleEntryNodeDto
                {
                    Scope = entry.Scope,
                    Key = entry.Key,
                    Prompt_part_id = entry.PromptPartId,
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
