using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// MCP read-only surface for the repositories slice (ADR-0024 § 8, T-13).
/// Slice 1 by design does not expose bind/unbind/sync via MCP — those remain
/// HTTP-only and UI-driven (see parent slice 0fad9876…, Q4 / D3). The single
/// tool here fans out across all bindings of an intent and serves the locally
/// persisted review-comments feed produced by the background sync (T-10).
/// </summary>
[McpServerToolType]
public sealed class RepositoryMcpTools(
    IIntentRepositoryBindingReader repositoryBindings,
    IPullRequestCommentRepository comments)
{
    [McpServerTool(Name = "list_intent_pr_comments", ReadOnly = true, UseStructuredContent = true)]
    [Description("List review comments from every repository binding attached to the intent, merged and ordered by 'created_at' ASC. Scope is review-comments only (issue-comments are out of slice 1, D3). When the optional 'since' filter is supplied (RFC 3339 timestamp), only comments with created_at >= since are returned. Bindings without an attached pull request contribute zero comments. The data is the locally cached feed populated by the background PR-sync service — call the HTTP POST .../sync endpoint to force a refresh.")]
    public async Task<McpIntentPrCommentsResult> ListIntentPrComments(
        [Description("Intent id whose attached repository bindings should be polled.")] string intent_id,
        [Description("Optional inclusive lower bound on PullRequestComment.created_at. RFC 3339 timestamp. Omit to read the full feed.")] DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var bindings = await repositoryBindings.ListByIntentAsync(intent_id, cancellationToken);
        if (bindings.Count == 0)
        {
            return new McpIntentPrCommentsResult([]);
        }

        var merged = new List<PullRequestCommentRecord>();
        foreach (var binding in bindings)
        {
            if (binding.State.PullRequestNumber is null)
            {
                continue;
            }
            var bindingComments = await comments.ListByBindingAsync(binding.Id, cancellationToken);
            if (bindingComments.Count == 0)
            {
                continue;
            }
            merged.AddRange(since is { } s
                ? bindingComments.Where(c => c.CreatedAt >= s)
                : bindingComments);
        }

        merged.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return new McpIntentPrCommentsResult(merged.Select(ToMcpComment).ToList());
    }

    private static McpIntentPrComment ToMcpComment(PullRequestCommentRecord record) => new(
        Id: record.UpstreamId,
        BindingId: record.BindingId.Value,
        AuthorLogin: record.AuthorLogin,
        Body: record.Body,
        CreatedAt: record.CreatedAt,
        UpdatedAt: record.UpdatedAt,
        HtmlUrl: record.HtmlUrl,
        Path: record.Path);
}
