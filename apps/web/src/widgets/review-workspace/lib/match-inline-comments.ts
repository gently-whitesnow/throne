import type { PullRequestComment } from "@/entities/pull-request-comment";
import type { DiffRow, PullRequestDiffFile } from "@/entities/review-workspace";

/**
 * Key a diff row by the (side, line) convention used for anchoring: a `del`
 * row anchors to its `oldLine` on the `left` side, every other row to its
 * `newLine` on the `right` side — mirroring `anchorFromRow` in the viewer.
 */
export function rowAnchorKey(row: DiffRow): string | null {
  if (row.kind === "del") {
    return row.oldLine === null ? null : `left:${String(row.oldLine)}`;
  }
  return row.newLine === null ? null : `right:${String(row.newLine)}`;
}

function commentAnchorKey(comment: PullRequestComment): string | null {
  if (comment.side == null || comment.line == null) return null;
  return `${comment.side}:${String(comment.line)}`;
}

/**
 * Buckets the file's inline-anchorable comments by row anchor key. A comment is
 * anchorable iff it targets this file's `path` and carries a non-null side+line;
 * everything else stays in the right rail only and is never returned here.
 */
export function indexInlineComments(
  file: PullRequestDiffFile,
  comments: PullRequestComment[]
): Map<string, PullRequestComment[]> {
  const byKey = new Map<string, PullRequestComment[]>();
  for (const comment of comments) {
    if (comment.path !== file.path) continue;
    const key = commentAnchorKey(comment);
    if (key === null) continue;
    const bucket = byKey.get(key);
    if (bucket) bucket.push(comment);
    else byKey.set(key, [comment]);
  }
  return byKey;
}
