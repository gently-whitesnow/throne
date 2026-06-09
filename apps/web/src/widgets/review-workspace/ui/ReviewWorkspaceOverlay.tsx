import { useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Loader2 } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";

import {
  deletePullRequestComment,
  syncPullRequest,
  updateReviewThread,
  usePullRequestComments,
  type PullRequestComment
} from "@/entities/pull-request-comment";
import {
  changeRequestKindLabel,
  hasPullRequest,
  intentRepositoriesQueryKeys,
  type RepositoryBinding
} from "@/entities/repository-binding";
import {
  mergePullRequest,
  type MergeStrategy
} from "@/entities/review-workspace";
import { useResizablePane } from "@/shared/lib";
import { ResizeHandle } from "@/shared/ui";

import { useMergeStatus } from "../model/use-merge-status";
import {
  useReviewWorkspace,
  type ReviewWorkspaceInitial,
  type ReviewWorkspaceState
} from "../model/use-review-workspace";
import type { CommentActions } from "./ReviewCommentCard";
import { ReviewDiffViewer } from "./ReviewDiffViewer";
import { ReviewFilesRail } from "./ReviewFilesRail";
import { ReviewMergeControl } from "./ReviewMergeControl";
import { ReviewRightRail } from "./ReviewRightRail";
import { ReviewScopeBar } from "./ReviewScopeBar";

const FILES_PANE = {
  key: "throne.review.rail.files",
  def: 288,
  min: 200,
  max: 560
};
const RIGHT_PANE = {
  key: "throne.review.rail.context",
  def: 320,
  min: 240,
  max: 560
};

interface ReviewWorkspaceOverlayProps {
  intentId: string;
  binding: RepositoryBinding;
  initial?: ReviewWorkspaceInitial;
  onStateChange?: (state: ReviewWorkspaceInitial) => void;
  onClose: () => void;
}

export function ReviewWorkspaceOverlay({
  intentId,
  binding,
  initial,
  onStateChange,
  onClose
}: ReviewWorkspaceOverlayProps) {
  const queryClient = useQueryClient();
  const ws = useReviewWorkspace(intentId, binding.id, initial);

  const filesPane = useResizablePane({
    storageKey: FILES_PANE.key,
    defaultWidth: FILES_PANE.def,
    min: FILES_PANE.min,
    max: FILES_PANE.max,
    edge: "right"
  });
  const rightPane = useResizablePane({
    storageKey: RIGHT_PANE.key,
    defaultWidth: RIGHT_PANE.def,
    min: RIGHT_PANE.min,
    max: RIGHT_PANE.max,
    edge: "left"
  });
  const { comments, isLoading, error, refresh } = usePullRequestComments(
    intentId,
    binding.id
  );
  const [syncing, setSyncing] = useState(false);
  const [mergeNonce, setMergeNonce] = useState(0);
  const [merging, setMerging] = useState(false);
  const [mergeError, setMergeError] = useState<string | null>(null);
  const mergeStatus = useMergeStatus(
    intentId,
    binding.id,
    hasPullRequest(binding),
    mergeNonce
  );

  const handleSync = useCallback(() => {
    setSyncing(true);
    void (async () => {
      try {
        await syncPullRequest(intentId, binding.id);
        void queryClient.invalidateQueries({
          queryKey: intentRepositoriesQueryKeys.list(intentId)
        });
        refresh();
        setMergeNonce((n) => n + 1);
      } finally {
        setSyncing(false);
      }
    })();
  }, [intentId, binding.id, queryClient, refresh]);

  const handleMerge = useCallback(
    (strategy: MergeStrategy, deleteBranch: boolean) => {
      setMerging(true);
      setMergeError(null);
      void (async () => {
        try {
          await mergePullRequest(intentId, binding.id, {
            strategy,
            delete_branch: deleteBranch
          });
          handleSync();
        } catch (err) {
          setMergeError(err instanceof Error ? err.message : String(err));
        } finally {
          setMerging(false);
        }
      })();
    },
    [intentId, binding.id, handleSync]
  );

  // Delete + resolve/reopen act at the provider; on success we refresh the feed
  // so the new `resolved` state is read back (Throne keeps no local status).
  const commentActions = useMemo<CommentActions>(
    () => ({
      onDelete: async (comment: PullRequestComment) => {
        await deletePullRequestComment(
          intentId,
          binding.id,
          comment.id,
          comment.thread_id
        );
        refresh();
      },
      onToggleResolved: async (comment: PullRequestComment) => {
        if (comment.thread_id == null) return;
        await updateReviewThread(
          intentId,
          binding.id,
          comment.thread_id,
          comment.resolved !== true
        );
        refresh();
      }
    }),
    [intentId, binding.id, refresh]
  );

  // Зеркалим выбранный scope/commit/файл наверх (роут пишет их в URL), чтобы
  // перезагрузка и шаринг ссылки переоткрывали ревьюилку в том же состоянии.
  useEffect(() => {
    onStateChange?.({
      scope: ws.scope,
      commitSha: ws.selectedCommitSha,
      path: ws.activePath
    });
  }, [onStateChange, ws.scope, ws.selectedCommitSha, ws.activePath]);

  // Esc закрывает, фон страницы не скроллится, пока открыт fullscreen.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = prevOverflow;
    };
  }, [onClose]);

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Review workspace"
      className="fixed inset-0 z-50 flex flex-col bg-base-100"
    >
      <ReviewScopeBar
        binding={binding}
        scope={ws.scope}
        selectedCommitSha={ws.selectedCommitSha}
        commits={ws.commits}
        commitsLoading={ws.commitsLoading}
        mergeControl={
          hasPullRequest(binding) ? (
            <ReviewMergeControl
              kind={changeRequestKindLabel(binding.provider)}
              status={mergeStatus.status}
              statusLoading={mergeStatus.loading}
              merging={merging}
              mergeError={mergeError}
              onMerge={handleMerge}
            />
          ) : undefined
        }
        onSelectRequest={ws.selectRequestScope}
        onSelectCommit={ws.selectCommit}
        onClose={onClose}
      />
      <div className="flex min-h-0 flex-1">
        <div
          className="min-h-0 shrink-0 border-r border-base-300 max-md:!w-auto"
          style={{ width: filesPane.width }}
        >
          <ReviewFilesRail
            files={ws.files}
            activePath={ws.activePath}
            onSelect={ws.selectFile}
            onAdjacent={ws.goToAdjacentFile}
          />
        </div>
        <ResizeHandle
          ariaLabel="Изменить ширину списка файлов"
          onPointerDown={filesPane.onPointerDown}
        />
        <main className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
          <DiffRegion
            ws={ws}
            intentId={intentId}
            bindingId={binding.id}
            comments={comments}
            commentActions={commentActions}
            onSubmitted={handleSync}
          />
        </main>
        <ResizeHandle
          ariaLabel="Изменить ширину панели контекста"
          onPointerDown={rightPane.onPointerDown}
        />
        <div
          className="min-h-0 shrink-0 max-md:!w-auto"
          style={{ width: rightPane.width }}
        >
          <ReviewRightRail
            comments={comments}
            commentsLoading={isLoading}
            commentsError={error}
            syncing={syncing}
            onSync={handleSync}
            commentActions={commentActions}
            onJump={(c) => {
              if (c.path == null) return;
              ws.jumpToComment(c.path, c.side ?? null, c.line ?? null);
            }}
          />
        </div>
      </div>
    </div>,
    document.body
  );
}

function DiffRegion({
  ws,
  intentId,
  bindingId,
  comments,
  commentActions,
  onSubmitted
}: {
  ws: ReviewWorkspaceState;
  intentId: string;
  bindingId: string;
  comments: PullRequestComment[];
  commentActions: CommentActions;
  onSubmitted: () => void;
}) {
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
