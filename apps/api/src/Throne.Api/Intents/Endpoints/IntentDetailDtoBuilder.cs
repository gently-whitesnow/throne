using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentDetailDtoBuilder
{
    public static async Task<IntentDetailDto> BuildAsync(Intent intent, IntentsApiHelpers helpers, CancellationToken ct)
    {
        var tagMap = await helpers.BuildTagMapAsync(intent.TagIds, ct);
        var pinnedIn = await helpers.GetPinnedInAsync(intent.Id.Value, ct);
        return IntentDtoMapper.ToDetailDto(intent, tagMap, pinnedIn: pinnedIn);
    }
}
