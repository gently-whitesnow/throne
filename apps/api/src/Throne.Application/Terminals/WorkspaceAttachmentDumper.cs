using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Dumps every attachment of an intent (image and text alike) as bytes into the session workspace on
/// spawn, so the embedded agent opens them with a native <c>Read</c>. The directory is reset first
/// (see <see cref="WorkspaceStagingReset"/>), so this only writes a fresh set — an attachment removed upstream simply does not reappear. Live
/// add-after-start is out of scope: new attachments land on the next spawn.
/// </summary>
public sealed class WorkspaceAttachmentDumper(IIntentAttachmentRepository attachments)
{
    public async Task DumpAsync(IntentId intentId, string workspacePath, CancellationToken ct)
    {
        var list = await attachments.ListByIntentAsync(intentId, ct);
        if (list.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(WorkspaceAttachmentPaths.DirectoryPath(workspacePath));
        foreach (var attachment in list)
        {
            var content = await attachments.OpenContentAsync(intentId, attachment.Id, ct);
            if (content is null)
            {
                continue;
            }

            await using var stream = content.Content;
            var path = WorkspaceAttachmentPaths.AbsolutePath(workspacePath, attachment.Id, attachment.FileName);
            await using var file = File.Create(path);
            await stream.CopyToAsync(file, ct);
        }
    }
}
