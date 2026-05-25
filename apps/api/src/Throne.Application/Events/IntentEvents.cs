using Throne.Application.Git;
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
/// Intent ↔ repository binding lifecycle events (ADR-0024). Translated into the
/// contract-first <c>intent.repository_bound</c> / <c>intent.repository_unbound</c> /
/// <c>intent.repository_clone_progress</c> / <c>intent.pr_comment_added</c> wire events.
/// </summary>
public sealed record IntentRepositoryBound(IntentRepositoryBinding Binding) : IDomainEvent;

public sealed record IntentRepositoryUnbound(IntentRepositoryBinding Binding) : IDomainEvent;

/// <summary>
/// Emitted on every <c>clone_status</c> transition
/// (<c>pending → cloning</c>, <c>cloning → ready</c>, <c>cloning → failed</c>) and by the
/// startup recovery pass when <c>cloning</c> bindings are flipped to
/// <c>failed("interrupted")</c>. Name follows the realtime-coverage gate's
/// PascalCase convention (<c>intent.repository_clone_progress</c> →
/// <c>IntentRepositoryCloneProgress</c>) so events.yaml and this record stay in lock-step.
/// </summary>
public sealed record IntentRepositoryCloneProgress(IntentRepositoryBinding Binding) : IDomainEvent;

public sealed record RepositoryPullRequestSynced(
    IntentRepositoryBinding Binding,
    int CommentCount) : IDomainEvent;

/// <summary>
/// Previously-unseen review comment observed for the binding's pull request.
/// The payload travels with the full upstream body in-memory and is NOT persisted
/// (pointer-only storage — GitHub is the source of truth).
/// </summary>
public sealed record IntentPrCommentAdded(
    IntentRepositoryBinding Binding,
    PullRequestComment Comment) : IDomainEvent;
