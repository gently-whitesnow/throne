using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Events;

public sealed record IntentCreated(Intent Intent) : IDomainEvent;

public sealed record IntentDeleted(string IntentId) : IDomainEvent;

public sealed record IntentTextChanged(Intent Intent) : IDomainEvent;

public sealed record IntentStatusChanged(Intent Intent) : IDomainEvent;

public sealed record IntentQaAdded(IntentQa Qa) : IDomainEvent;

public sealed record IntentReviewAdded(IntentReview Review) : IDomainEvent;

public sealed record IntentAttachmentAdded(IntentAttachment Attachment) : IDomainEvent;

public sealed record IntentAttachmentDeleted(string IntentId, string AttachmentId) : IDomainEvent;
