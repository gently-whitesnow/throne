import { AlertCircle, Loader2 } from "lucide-react";

import type { PullRequestComment } from "@/entities/pull-request-comment";

import type { ReviewWorkspaceState } from "../model/use-review-workspace";
import type { CommentActions } from "./ReviewCommentCard";
import { ReviewDiffViewer } from "./ReviewDiffViewer";

interface ReviewDiffRegionProps {
  ws: ReviewWorkspaceState;
  intentId: string;
  bindingId: string;
  comments: PullRequestComment[];
  commentActions: CommentActions;
  onSubmitted: () => void;
}

export function ReviewDiffRegion({
  ws,
  intentId,
  bindingId,
  comments,
  commentActions,
  onSubmitted
}: ReviewDiffRegionProps) {
  if (ws.diffLoading) {
    return (
      <p className="flex items-center gap-2 px-4 py-6 text-xs text-base-content/60">
        <Loader2
          aria-hidden
          size={14}
          strokeWidth={2}
          className="animate-spin"
        />
        Загружаем diff…
      </p>
    );
  }
  if (ws.diffError !== null) {
    return (
      <p
        role="alert"
        className="m-4 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-error"
      >
        <AlertCircle aria-hidden size={14} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить diff: {ws.diffError.message}</span>
      </p>
    );
  }
  if (ws.activeFile === null || ws.anchorShas === null) {
    return (
      <p className="px-4 py-6 text-xs text-base-content/60">
        В этом diff нет файлов.
      </p>
    );
  }
  return (
    <>
      <div className="border-b border-base-300 bg-base-100 px-4 py-2">
        <span className="font-mono text-[13px] font-semibold text-base-content">
          {ws.activeFile.path}
        </span>
        {ws.activeFile.previous_path != null ? (
          <span className="ml-2 font-mono text-[11px] text-base-content/50">
            ← {ws.activeFile.previous_path}
          </span>
        ) : null}
      </div>
      <div className="min-h-0 flex-1 overflow-auto">
        <ReviewDiffViewer
          key={`${ws.activeFile.path}:${ws.anchorShas.commit_sha}`}
          file={ws.activeFile}
          shas={ws.anchorShas}
          intentId={intentId}
          bindingId={bindingId}
          comments={comments}
          commentActions={commentActions}
          scrollTarget={ws.scrollTarget}
          onSubmitted={onSubmitted}
        />
      </div>
    </>
  );
}
