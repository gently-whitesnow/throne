import { useEffect, useMemo, useState } from "react";

import { useIntent } from "@/entities/intent";
import {
  REVIEW_RECOMMENDATION_ARTIFACT_TYPE,
  usePullRequestArtifactQuery
} from "@/entities/pull-request-artifact";
import {
  deletePullRequestComment,
  updateReviewThread,
  usePullRequestComments,
  type PullRequestComment
} from "@/entities/pull-request-comment";
import {
  changeRequestKindLabel,
  hasPullRequest,
  repositoryFullName,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { OpenBindingInVscodeButton } from "@/features/open-in-vscode";
import { useResizablePane } from "@/shared/lib";
import { Modal, ResizeHandle } from "@/shared/ui";

import { orderFilesByAi } from "../lib/order-files-by-ai";
import { useMergeOrchestration } from "../model/use-merge-orchestration";
import { useMergeStatus } from "../model/use-merge-status";
import {
  useReviewWorkspace,
  type ReviewWorkspaceInitial
} from "../model/use-review-workspace";
import type { CommentActions } from "./ReviewCommentCard";
import { ReviewArtifactStaleBanner } from "./ReviewArtifactStaleBanner";
import { ReviewDiffRegion } from "./ReviewDiffRegion";
import { ReviewFilesRail, type ReviewFilesSortMode } from "./ReviewFilesRail";
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
  const artifactQuery = usePullRequestArtifactQuery(
    binding.id,
    REVIEW_RECOMMENDATION_ARTIFACT_TYPE,
    hasPullRequest(binding)
  );
  const artifact = artifactQuery.data ?? null;
  const aiFileOrder = artifact?.review_recommendation?.file_order ?? null;
  const hasAiOrder = aiFileOrder !== null && aiFileOrder.length > 0;

  const [sortMode, setSortMode] =
    useState<ReviewFilesSortMode>("ai-recommended");
  const effectiveAiOrder =
    sortMode === "ai-recommended" && hasAiOrder ? aiFileOrder : null;
  const ws = useReviewWorkspace(
    intentId,
    binding.id,
    initial,
    effectiveAiOrder
  );

  const aiOrderHints = useMemo(
    () => orderFilesByAi(ws.files, aiFileOrder).hints,
    [ws.files, aiFileOrder]
  );

  const stale =
    artifact?.head_sha != null &&
    ws.diff?.head_sha != null &&
    artifact.head_sha !== ws.diff.head_sha;

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

  // Same «Очистить состояние» flag (D1) as the intent page, seeded from the intent so the
  // checkbox reflects its current value; merge writes it via the intent endpoint and, when
  // cleared, additionally suppresses auto-close (D2) so the intent stays open after the merge.
  const intentQuery = useIntent(intentId);
  const [cleanupOverride, setCleanupOverride] = useState<boolean | null>(null);
  const cleanup =
    cleanupOverride ?? intentQuery.data?.cleanup_local_state_on_done ?? true;

  const { syncing, merging, mergeError, mergeNonce, handleSync, handleMerge } =
    useMergeOrchestration({
      intentId,
      bindingId: binding.id,
      cleanup,
      refreshComments: refresh
    });
  const mergeStatus = useMergeStatus(
    intentId,
    binding.id,
    hasPullRequest(binding),
    mergeNonce
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

  return (
    <Modal variant="fullscreen" ariaLabel="Review workspace" onClose={onClose}>
      <ReviewScopeBar
        binding={binding}
        scope={ws.scope}
        selectedCommitSha={ws.selectedCommitSha}
        commits={ws.commits}
        commitsLoading={ws.commitsLoading}
        openInVscode={
          <OpenBindingInVscodeButton
            intentId={intentId}
            bindingId={binding.id}
            fullName={repositoryFullName(binding)}
            disabled={binding.clone_status !== "ready"}
          />
        }
        mergeControl={
          hasPullRequest(binding) ? (
            <ReviewMergeControl
              kind={changeRequestKindLabel(binding.provider)}
              status={mergeStatus.status}
              statusLoading={mergeStatus.loading}
              merging={merging}
              mergeError={mergeError}
              cleanup={cleanup}
              onCleanupChange={setCleanupOverride}
              onMerge={handleMerge}
            />
          ) : undefined
        }
        onSelectRequest={ws.selectRequestScope}
        onSelectCommit={ws.selectCommit}
        onClose={onClose}
      />
      {stale ? <ReviewArtifactStaleBanner /> : null}
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
            sortMode={sortMode}
            onChangeSortMode={hasAiOrder ? setSortMode : undefined}
            aiOrderHints={aiOrderHints}
          />
        </div>
        <ResizeHandle
          ariaLabel="Изменить ширину списка файлов"
          onPointerDown={filesPane.onPointerDown}
        />
        <main className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
          <ReviewDiffRegion
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
            intentId={intentId}
            bindingId={binding.id}
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
    </Modal>
  );
}
