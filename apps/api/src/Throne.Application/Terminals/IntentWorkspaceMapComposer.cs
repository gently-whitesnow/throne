using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Terminals;

public sealed class IntentWorkspaceMapComposer(
    IWorkspaceRootProvider workspaceRoot,
    RunPreflightTagNames tagNames,
    IntentLinkPromptContextReader linkContext)
{
    public async Task<string> ComposePreviewAsync(
        Intent intent,
        IReadOnlyList<IntentRepositoryBinding> bindings,
        IReadOnlyList<IntentAttachment> attachments,
        IReadOnlyList<IntentCardAttachment> cardAttachments,
        IReadOnlyList<string> sessionSkillIds,
        CancellationToken ct)
    {
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intent.Id.Value);
        var repoPaths = bindings.Select(b => b.WorkspacePath).ToArray();
        var tags = await tagNames.ResolveAsync(intent.TagIds, ct);
        var links = await linkContext.BuildAsync(intent.Id, ct);
        return WorkspaceMapPrompt.Compose(
            workspacePath, repoPaths, tags, intent.State.Title, links,
            attachments, cardAttachments, sessionSkillIds, userPrompt: "").TrimEnd();
    }
}
