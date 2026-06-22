---
name: review
description: Use when reviewing the PR/MR attached to the current Throne Intent to write the single review_recommendation artifact through skills/review/bin/throne-review. Event-driven, triggered by PR/MR review work.
---

# Throne Review Recommendation

Use `skills/review/bin/throne-review write` to store the single `review_recommendation` artifact for the PR/MR attached to the current intent.

The script reads `THRONE_API_BASE`, `THRONE_INTENT_ID`, and optionally `THRONE_REPOSITORY_BINDING_ID`. If no binding id is in the environment, pass `--binding-id` or run `skills/review/bin/throne-review bindings`.

Write one JSON payload on stdin. The payload follows `PutPullRequestArtifactRequest` in `specs/contracts/repositories/openapi.yaml`.

```bash
skills/review/bin/throne-review write < /tmp/review-recommendation.json
```

Payload shape:

```json
{
  "render": "markdown",
  "content": "## Review recommendation\n...",
  "summary": "Short recommendation for the operator",
  "source": "agent",
  "source_refs": ["gh pr diff", "gh pr view --comments"],
  "head_sha": "<PR head commit sha>",
  "review_recommendation": {
    "file_order": [
      { "path": "src/Core.cs", "reason": "core/highest-risk; read first", "risk": "high" }
    ]
  },
  "produced_at": "2026-06-18T12:00:00Z"
}
```

Order `file_order` from risky/root files to leaves. `risk` is `high`, `medium`, or `low`. Do not invent typed fields not present in the contract.
