using Throne.Application.Ports;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkAuthor = Throne.Intents.Contracts.Generated.IntentLinkAuthor;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;
using DomainIntentLink = Throne.Domain.Intents.Linking.IntentLink;
using DomainIntentLinkAuthor = Throne.Domain.Intents.Linking.IntentLinkAuthor;
using DomainIntentLinkType = Throne.Domain.Intents.Linking.IntentLinkType;
using DomainLinkDirection = Throne.Application.Ports.IntentLinkDirection;

namespace Throne.Api.Intents;

internal static class IntentLinkDtoMapper
{
    public static IntentLinkDto ToLinkDto(DomainIntentLink link) => new()
    {
        Id = link.Id,
        From_id = link.FromId.Value,
        To_id = link.ToId.Value,
        Type = ToContractLinkType(link.Type),
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

    public static ContractIntentLinkType ToContractLinkType(string type) => type switch
    {
        DomainIntentLinkType.Relates => ContractIntentLinkType.Relates,
        DomainIntentLinkType.Blocks => ContractIntentLinkType.Blocks,
        DomainIntentLinkType.DerivedFrom => ContractIntentLinkType.Derived_from,
        DomainIntentLinkType.DuplicateOf => ContractIntentLinkType.Duplicate_of,
        _ => throw new InvalidOperationException($"Unknown domain link type: {type}"),
    };

    public static string FromContractLinkType(ContractIntentLinkType type) => type switch
    {
        ContractIntentLinkType.Relates => DomainIntentLinkType.Relates,
        ContractIntentLinkType.Blocks => DomainIntentLinkType.Blocks,
        ContractIntentLinkType.Derived_from => DomainIntentLinkType.DerivedFrom,
        ContractIntentLinkType.Duplicate_of => DomainIntentLinkType.DuplicateOf,
        _ => throw new InvalidOperationException($"Unknown contract link type: {type}"),
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
