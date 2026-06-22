using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Domain.Dreams;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.PromptParts;
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

/// <summary>
/// Repository registry + knowledge-page lifecycle (ADR-0031).
/// <see cref="RepositoryRegistered"/> marks the first materialisation of a coordinate and is
/// carried by <see cref="Throne.Application.Ports.EnsureRepositoryOutcome.Created"/> (aggregated by
/// the bind / tag-default / page-write paths that ensure the registry); it has no realtime
/// projection. <see cref="RepositoryDocumentUpdated"/> rides on
/// <see cref="Throne.Application.Ports.WriteRepositoryArtifactOutcome.Written"/> and is translated
/// into the contract-first <c>repository.document_updated</c> wire event.
/// </summary>
public sealed record RepositoryRegistered(Repository Repository) : IDomainEvent;

public sealed record RepositoryDocumentUpdated(RepositoryArtifact Artifact) : IDomainEvent;

public sealed record PullRequestArtifactUpdated(PullRequestArtifact Artifact) : IDomainEvent;

public sealed record TagCreated(Tag Tag) : IDomainEvent;

public sealed record TagUpdated(Tag Tag) : IDomainEvent;

public sealed record TagDeleted(string TagId) : IDomainEvent;

/// <summary>
/// PromptPartPatch lifecycle (ADR-0036). Carried by
/// <see cref="Throne.Application.Ports.CreatePromptPartPatchOutcome"/>,
/// <see cref="Throne.Application.Ports.ApplyPromptPartPatchPersistenceOutcome"/> and
/// <see cref="Throne.Application.Ports.RejectPromptPartPatchPersistenceOutcome"/>; the
/// dispatching unit-of-work decorator fans them out after a successful commit.
/// </summary>
public sealed record PromptPartPatchProposed(PromptPartPatch Patch) : IDomainEvent;

public sealed record PromptPartPatchApplied(PromptPartPatch Patch) : IDomainEvent;

public sealed record PromptPartPatchRejected(PromptPartPatch Patch) : IDomainEvent;

public sealed record PromptPartPatchSuperseded(PromptPartPatch Patch) : IDomainEvent;

/// <summary>
/// A frontier agent finished a /dream pass and recorded its memory of it via
/// <c>skills/dream/bin/throne-dream record-session</c>. Carried by
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

/// <summary>
/// Embedded-terminal liveness hints (Slice 2, ADR-0026). tmux is the single source of
/// truth for whether a session is alive; these events are best-effort so the
/// "terminal running" sidebar bucket updates without polling. Unlike the rest of the
/// realtime events they are NOT carried by a Mongo write outcome — the spawn / kill /
/// natural-exit paths dispatch them directly via <see cref="IDomainEventDispatcher"/>.
/// A lost event self-heals: <c>/intents/contexts</c> re-queries tmux on every refresh.
/// </summary>
public sealed record TerminalSessionStarted(string IntentId) : IDomainEvent;

public sealed record TerminalSessionStopped(string IntentId) : IDomainEvent;

/// <summary>
/// The post-spawn background prompt delivery (<see cref="Terminals.RunPreflightPromptDelivery"/>)
/// could not confirm the initial prompt was submitted — the TUI never became ready, or the trailing
/// Enter was not acknowledged within the confirm budget. The tmux session is alive (spawn succeeded);
/// this is a best-effort UX hint so the front can prompt the operator to check the composer and
/// resend, NOT a spawn failure. A lost event is harmless — the live pane shows the real state.
/// </summary>
public sealed record TerminalPromptSubmitUnconfirmed(string IntentId) : IDomainEvent;
