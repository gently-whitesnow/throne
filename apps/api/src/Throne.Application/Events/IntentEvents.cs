using Throne.Application.ChatUploads;
using Throne.Application.Intents;
using Throne.Domain.DreamRuns;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;

namespace Throne.Application.Events;

public sealed record IntentCreated(Intent Intent) : IDomainEvent;

public sealed record IntentDeleted(string IntentId) : IDomainEvent;

public sealed record IntentTextChanged(Intent Intent) : IDomainEvent;

public sealed record IntentStatusChanged(Intent Intent) : IDomainEvent;

public sealed record IntentTagsChanged(Intent Intent) : IDomainEvent;

public sealed record IntentQaAdded(IntentQa Qa) : IDomainEvent;

public sealed record IntentReviewAdded(IntentReview Review) : IDomainEvent;

public sealed record IntentAttachmentAdded(IntentAttachment Attachment) : IDomainEvent;

public sealed record IntentAttachmentDeleted(string IntentId, string AttachmentId) : IDomainEvent;

public sealed record TagCreated(Tag Tag) : IDomainEvent;

public sealed record TagUpdated(Tag Tag) : IDomainEvent;

public sealed record TagDeleted(string TagId) : IDomainEvent;

public sealed record DreamRunCreated(DreamRun Run) : IDomainEvent;

public sealed record DreamProposalCreated(DreamRun Run, DreamProposal Proposal) : IDomainEvent;

public sealed record DreamProposalApplied(DreamRun Run, DreamProposal Proposal) : IDomainEvent;

public sealed record DreamProposalSkipped(DreamRun Run, DreamProposal Proposal) : IDomainEvent;

public sealed record DreamRunClosed(DreamRun Run) : IDomainEvent;

/// <summary>
/// Best-effort, debounced fuel-meter signal. Carrying repos do NOT raise this; it's
/// produced by an out-of-band debouncer that observes meaningful evidence writes.
/// UI may also call <c>GET /api/v1/dream-runs/readiness</c> for an authoritative snapshot.
/// </summary>
public sealed record DreamFuelChanged(int AvailableTokens, string Status) : IDomainEvent;

public sealed record ChatUploadCreated(ChatUpload Upload) : IDomainEvent;

public sealed record ChatUploadDeleted(string ChatUploadId) : IDomainEvent;
