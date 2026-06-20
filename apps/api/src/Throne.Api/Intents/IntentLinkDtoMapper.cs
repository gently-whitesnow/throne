using Throne.Application.Ports;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkAuthor = Throne.Intents.Contracts.Generated.IntentLinkAuthor;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using DomainIntentLink = Throne.Domain.Intents.Linking.IntentLink;
using DomainIntentLinkAuthor = Throne.Domain.Intents.Linking.IntentLinkAuthor;
using DomainLinkDirection = Throne.Application.Ports.IntentLinkDirection;

namespace Throne.Api.Intents;

internal static class IntentLinkDtoMapper
{
    public static IntentLinkDto ToLinkDto(DomainIntentLink link) => new()
    {
        Id = link.Id,
        From_id = link.FromId.Value,
        To_id = link.ToId.Value,
        Blocking = link.Blocking,
        Author = ToContractLinkAuthor(link.Author),
        Rationale = link.Rationale,
        Created_at = link.CreatedAt,
    };

    public static IntentLinkViewDto ToLinkViewDto(IntentLinkView view, IReadOnlyDictionary<string, Tag> tagsById) => new()
    {
        Link = ToLinkDto(view.Link),
        Direction = view.Direction == DomainLinkDirection.Outgoing
            ? ContractIntentLinkDirection.Outgoing
            : ContractIntentLinkDirection.Incoming,
        Peer = IntentDtoMapper.ToPeerDto(view.Other, tagsById),
    };

    public static DomainIntentLinkAuthor FromContractLinkAuthor(ContractIntentLinkAuthor author) => author switch
    {
        ContractIntentLinkAuthor.User => DomainIntentLinkAuthor.User,
        ContractIntentLinkAuthor.Agent => DomainIntentLinkAuthor.Agent,
        _ => throw new InvalidOperationException($"Unknown contract link author: {author}"),
    };

    public static ContractIntentLinkAuthor ToContractLinkAuthor(DomainIntentLinkAuthor author) => author switch
    {
        DomainIntentLinkAuthor.User => ContractIntentLinkAuthor.User,
        DomainIntentLinkAuthor.Agent => ContractIntentLinkAuthor.Agent,
        _ => throw new InvalidOperationException($"Unknown domain link author: {author}"),
    };
}
