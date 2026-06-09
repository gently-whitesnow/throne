using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Reads review-thread resolution state via the GitHub GraphQL API — REST
/// <c>/pulls/{n}/comments</c> carries neither the thread node id nor
/// <c>isResolved</c>. Best-effort: enrichment is additive, so any failure or
/// missing data yields an empty map rather than breaking the comments feed.
/// </summary>
internal sealed class GhReviewThreadsReader(GhCliInvoker gh, ILogger<GhReviewThreadsReader> logger)
{
    private const int PageCap = 100;

    private const string Query =
        "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name)"
        + "{pullRequest(number:$number){reviewThreads(first:100){nodes{id isResolved "
        + "comments(first:100){nodes{databaseId}}}}}}}";

    public async Task<IReadOnlyDictionary<string, ThreadResolution>> ReadAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        try
        {
            var args = new[]
            {
                "api", "graphql",
                "-f", $"query={Query}",
                "-f", $"owner={owner}",
                "-f", $"name={repo}",
                "-F", $"number={number}",
            };
            var result = await gh.RunAsync(args, ct);
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Empty;
            }
            return Parse(result.StandardOutput, owner, repo, number);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GhReviewThreadsReaderLog.ReadFailed(logger, owner, repo, number, ex);
            return Empty;
        }
    }

    private IReadOnlyDictionary<string, ThreadResolution> Parse(string json, string owner, string repo, int number)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        // A graphql 200 with a top-level `errors` array means the query failed.
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            return Empty;
        }
        if (!TryGetThreads(root, out var threads))
        {
            return Empty;
        }

        var map = new Dictionary<string, ThreadResolution>();
        if (threads.GetArrayLength() >= PageCap)
        {
            GhReviewThreadsReaderLog.ThreadsCapped(logger, PageCap, owner, repo, number);
        }
        foreach (var thread in threads.EnumerateArray())
        {
            ProjectThread(thread, map, owner, repo, number);
        }
        return map;
    }

    private void ProjectThread(
        JsonElement thread,
        Dictionary<string, ThreadResolution> map,
        string owner,
        string repo,
        int number)
    {
        var threadId = GhJson.String(thread, "id");
        if (threadId is null
            || !thread.TryGetProperty("comments", out var comments)
            || !comments.TryGetProperty("nodes", out var nodes)
            || nodes.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        var resolved = GhJson.Bool(thread, "isResolved");
        if (nodes.GetArrayLength() >= PageCap)
        {
            GhReviewThreadsReaderLog.CommentsCapped(logger, threadId, owner, repo, number, PageCap);
        }
        foreach (var node in nodes.EnumerateArray())
        {
            var databaseId = GhJson.Int(node, "databaseId");
            if (databaseId is not null)
            {
                map[databaseId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                    new ThreadResolution(threadId, resolved);
            }
        }
    }

    private static bool TryGetThreads(JsonElement root, out JsonElement threads)
    {
        threads = default;
        return root.TryGetProperty("data", out var data)
            && data.TryGetProperty("repository", out var repository)
            && repository.ValueKind == JsonValueKind.Object
            && repository.TryGetProperty("pullRequest", out var pr)
            && pr.ValueKind == JsonValueKind.Object
            && pr.TryGetProperty("reviewThreads", out var reviewThreads)
            && reviewThreads.TryGetProperty("nodes", out threads)
            && threads.ValueKind == JsonValueKind.Array;
    }

    private static readonly IReadOnlyDictionary<string, ThreadResolution> Empty =
        new Dictionary<string, ThreadResolution>();
}

/// <summary>Thread node id + resolution flag joined onto a review comment by its REST id.</summary>
internal readonly record struct ThreadResolution(string ThreadId, bool Resolved);
