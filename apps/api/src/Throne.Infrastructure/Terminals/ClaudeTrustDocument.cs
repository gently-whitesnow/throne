using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Pure transform over the agent CLI's <c>~/.claude.json</c> document: set
/// <c>projects["&lt;path&gt;"].hasTrustDialogAccepted = true</c> while preserving every other
/// key. Kept I/O-free so the merge semantics (the part that could corrupt the operator's
/// global config) are fully unit-testable; <see cref="ClaudeWorkspaceTrust"/> wraps it with
/// the file read/write.
/// </summary>
internal static class ClaudeTrustDocument
{
    private const string ProjectsKey = "projects";
    private const string TrustKey = "hasTrustDialogAccepted";

    // Match the file the agent CLI writes: 2-space indent and readable (unescaped) unicode,
    // so seeding trust does not rewrite half the file as \uXXXX and explode the diff.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Returns the updated document text when <paramref name="workspacePath"/> needed
    /// trusting, or <c>null</c> when no write is warranted: already trusted, or the existing
    /// document is shaped in a way we refuse to clobber (unparseable, non-object root, or a
    /// non-object <c>projects</c> / project entry). <paramref name="existingJson"/> may be
    /// <c>null</c>/empty for a not-yet-created file — treated as an empty object.
    /// </summary>
    public static string? WithTrustedWorkspace(string? existingJson, string workspacePath)
    {
        var root = ParseRoot(existingJson);
        if (root is null)
        {
            return null;
        }

        if (root[ProjectsKey] is not JsonObject projects)
        {
            if (root.ContainsKey(ProjectsKey))
            {
                return null;
            }
            projects = [];
            root[ProjectsKey] = projects;
        }

        if (projects[workspacePath] is JsonObject existingEntry)
        {
            if (IsTrueFlag(existingEntry[TrustKey]))
            {
                return null;
            }
            existingEntry[TrustKey] = true;
        }
        else
        {
            projects[workspacePath] = new JsonObject { [TrustKey] = true };
        }

        return root.ToJsonString(SerializerOptions);
    }

    private static JsonObject? ParseRoot(string? existingJson)
    {
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(existingJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTrueFlag(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.True;
}
