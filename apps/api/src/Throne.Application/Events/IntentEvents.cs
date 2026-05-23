using Throne.Application.Intents;
using Throne.Domain.Dreams;
using Throne.Domain.Instructions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Application.Events;

public sealed record IntentCreated(Intent Intent) : IDomainEvent;

public sealed record IntentDeleted(string IntentId) : IDomainEvent;

public sealed record IntentTextChanged(Intent Intent) : IDomainEvent;

public sealed record IntentStatusChanged(Intent Intent) : IDomainEvent;

public sealed record IntentTagsChanged(Intent Intent) : IDomainEvent;

public sealed record IntentReordered(Intent Intent) : IDomainEvent;

public sealed record IntentPinned(string IntentId, string ContextTagId, string PinSortKey) : IDomainEvent;

public sealed record IntentUnpinned(string IntentId, string ContextTagId) : IDomainEvent;

public sealed record IntentPinMoved(string IntentId, string ContextTagId, string PinSortKey) : IDomainEvent;

public sealed record IntentLinkAdded(IntentLink Link) : IDomainEvent;

public sealed record IntentLinkRemoved(IntentLink Link) : IDomainEvent;

public sealed record IntentAttachmentAdded(IntentAttachment Attachment) : IDomainEvent;

public sealed record IntentAttachmentDeleted(string IntentId, string AttachmentId) : IDomainEvent;

public sealed record TagCreated(Tag Tag) : IDomainEvent;

public sealed record TagUpdated(Tag Tag) : IDomainEvent;

public sealed record TagDeleted(string TagId) : IDomainEvent;

/// <summary>
/// InstructionPatch lifecycle (ADR-0021 supersedes ADR-0011). Carried by
/// <see cref="Throne.Application.Ports.CreateInstructionPatchOutcome"/>,
/// <see cref="Throne.Application.Ports.ApplyInstructionPatchPersistenceOutcome"/> and
/// <see cref="Throne.Application.Ports.RejectInstructionPatchPersistenceOutcome"/>; the
/// dispatching unit-of-work decorator fans them out after a successful commit.
/// </summary>
public sealed record InstructionPatchProposed(InstructionPatch Patch) : IDomainEvent;

public sealed record InstructionPatchApplied(InstructionPatch Patch) : IDomainEvent;

public sealed record InstructionPatchRejected(InstructionPatch Patch) : IDomainEvent;

public sealed record InstructionPatchSuperseded(InstructionPatch Patch) : IDomainEvent;

/// <summary>
/// A frontier agent finished a /dream pass and recorded its memory of it via
/// <c>mcp__throne__record_dream_session</c>. Carried by
/// <see cref="Throne.Application.Ports.CreateDreamSessionOutcome"/>.
/// </summary>
public sealed record DreamSessionRecorded(DreamSession Session) : IDomainEvent;

/// <summary>
/// Intent ↔ repository binding lifecycle events (ADR-0024, slice 1 T-08+). Carried by the
/// repository outcomes in <see cref="Throne.Application.Ports.IIntentRepositoryBindingRepository"/>
/// (Bound / Unbound) and by <see cref="Throne.Application.Repositories.SyncRepositoryPullRequestResult"/>
/// (Synced). T-12 realtime emitters subscribe and translate to the contract-first
/// <c>intent.repository_bound</c> / <c>intent.repository_unbound</c> /
/// <c>intent.repository_clone_progress</c> / <c>intent.pr_comment_added</c> wire events.
/// Per-comment <c>intent.pr_comment_added</c> fanout is owned by the background
/// poller (T-10) which holds the comment store; manual refresh from T-08 returns
/// comments synchronously to the caller.
/// </summary>
public sealed record IntentRepositoryBound(IntentRepositoryBinding Binding) : IDomainEvent;

public sealed record IntentRepositoryUnbound(IntentRepositoryBinding Binding) : IDomainEvent;

public sealed record RepositoryPullRequestSynced(
    IntentRepositoryBinding Binding,
    int CommentCount) : IDomainEvent;
