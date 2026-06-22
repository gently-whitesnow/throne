using Microsoft.AspNetCore.Http;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;

namespace Throne.Api.Shared;

internal static class ApiErrorRegistry
{
    private static readonly Dictionary<string, int> StatusByCode = new()
    {
        [ErrorCodes.TerminalArgsInvalid] = StatusCodes.Status400BadRequest,

        [ErrorCodes.IntentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.IntentAttachmentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.TagNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PromptPartPatchNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DreamSessionNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RepositoryBindingNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RepositoryUpstreamGone] = StatusCodes.Status404NotFound,
        [ErrorCodes.RepositoryBlobNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RepositoryArtifactNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PullRequestArtifactNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RepositoryNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.CapabilityNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.CapabilityProviderNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PromptPartNotFound] = StatusCodes.Status404NotFound,

        [ErrorCodes.IntentVersionConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.TagVersionConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.TagNameTaken] = StatusCodes.Status409Conflict,
        [ErrorCodes.TagInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.PromptPartPatchAlreadyDecided] = StatusCodes.Status409Conflict,
        [ErrorCodes.PromptPartPatchNeedsRebase] = StatusCodes.Status409Conflict,
        [ErrorCodes.RepositoryBindingAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.RepositoryNotReady] = StatusCodes.Status409Conflict,
        [ErrorCodes.RepositoryPullRequestNotAttached] = StatusCodes.Status409Conflict,
        [ErrorCodes.RepositoryPullRequestAlreadyAttached] = StatusCodes.Status409Conflict,
        [ErrorCodes.RepositoryPullRequestMergeRejected] = StatusCodes.Status409Conflict,
        [ErrorCodes.RepositoryArtifactVersionConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.TerminalSessionAlreadyRunning] = StatusCodes.Status409Conflict,
        [ErrorCodes.PromptPartAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.PromptPartVersionConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.PromptPartHasRoles] = StatusCodes.Status409Conflict,
        [LinkErrorCodes.Duplicate] = StatusCodes.Status409Conflict,

        [ErrorCodes.IntentAttachmentTooLarge] = StatusCodes.Status413PayloadTooLarge,

        [ErrorCodes.IntentTextMatchNotFound] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IntentTextMatchAmbiguous] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IntentTextLineOutOfRange] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ValidationFailed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IntentAttachmentLimitExceeded] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TagNameInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RepositoryProviderUnsupported] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RepositoryProviderNotAuthenticated] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RepositoryReviewAnchorInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RepositoryCoordinateInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CapabilityDisabled] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IdeProviderUnavailable] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.GitLabHostInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TerminalRunPreflightBlocked] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TerminalSpawnFailed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TerminalCloneWaitTimeout] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TerminalModeInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PromptPartTextMatchFailed] = StatusCodes.Status422UnprocessableEntity,
        [LinkErrorCodes.SelfLink] = StatusCodes.Status422UnprocessableEntity,

        [ErrorCodes.RepositoryWorkspaceRemovalFailed] = StatusCodes.Status500InternalServerError,
    };

    public static int GetStatus(string code) =>
        StatusByCode.TryGetValue(code, out var status)
            ? status
            : StatusCodes.Status500InternalServerError;
}
