import type { PullRequestComment } from "@/entities/pull-request-comment";

import { ReviewCommentCard, type CommentActions } from "./ReviewCommentCard";

/**
 * Existing review comments anchored to a single diff row, rendered as a thread
 * card stack directly under that row. File path is hidden here — the row makes
 * the location obvious — so cards stay compact.
 */
export function ReviewInlineThread({
  comments,
  actions
}: {
  comments: PullRequestComment[];
  actions: CommentActions;
}) {
  return (
    <div className="flex flex-col gap-1.5 border-l-2 border-base-300 bg-base-200/40 px-3 py-2">
      {comments.map((comment) => (
        <ReviewCommentCard
          key={comment.id}
          comment={comment}
          actions={actions}
          showFile={false}
        />
      ))}
    </div>
  );
}
