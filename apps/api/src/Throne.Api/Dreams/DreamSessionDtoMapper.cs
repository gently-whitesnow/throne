using Throne.Application.Dreams;
using Throne.Application.Ports;
using Throne.Domain.Dreams;
using Throne.Dreams.Contracts.Generated;

namespace Throne.Api.Dreams;

/// <summary>
/// Boundary mapper between <see cref="DreamSession"/> / <see cref="DreamSourceEntry"/>
/// and the generated OpenAPI DTOs. Realtime fanout reuses <see cref="ToDto"/>
/// through <c>RealtimeDomainEventHandler</c>.
/// </summary>
public static class DreamSessionDtoMapper
{
    public static DreamSessionDto ToDto(DreamSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var dto = new DreamSessionDto
        {
            Id = session.Id,
            Created_at = session.Identity.CreatedAt,
            Vendor = session.Payload.Vendor,
            Processed_conversation_ids = session.Payload.ProcessedConversationIds.ToList(),
            Summary = session.Payload.Summary,
            Proposed_patch_ids = session.Payload.ProposedPatchIds.ToList(),
        };
        if (session.Payload.DateFrom is { } from)
        {
            dto.Date_from = from;
        }
        if (session.Payload.DateTo is { } to)
        {
            dto.Date_to = to;
        }
        if (session.Payload.Reflection is { } reflection)
        {
            dto.Reflection = reflection;
        }
        return dto;
    }

    public static DreamSessionPageDto ToPageDto(DreamSessionPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new DreamSessionPageDto
        {
            Items = page.Items.Select(ToDto).ToList(),
            Next_cursor = page.NextCursor,
        };
    }

    public static DreamSourceDto ToSourceDto(DreamSourceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new DreamSourceDto
        {
            Vendor = entry.Vendor,
            Path = entry.Path,
            Hint = entry.Hint,
        };
    }

    public static DreamSourcePageDto ToSourcePageDto(IReadOnlyList<DreamSourceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new DreamSourcePageDto
        {
            Items = entries.Select(ToSourceDto).ToList(),
        };
    }
}
