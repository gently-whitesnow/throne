import { useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Loader2 } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { createPortal } from "react-dom";

import {
  syncPullRequest,
  usePullRequestComments
} from "@/entities/pull-request-comment";
import {
  intentRepositoriesQueryKeys,
  type RepositoryBinding
} from "@/entities/repository-binding";

import { useReviewWorkspace } from "../model/use-review-workspace";
import { ReviewDiffViewer } from "./ReviewDiffViewer";
import { ReviewFilesRail } from "./ReviewFilesRail";
import { ReviewRightRail } from "./ReviewRightRail";
import { ReviewScopeBar } from "./ReviewScopeBar";

interface ReviewWorkspaceOverlayProps {
  intentId: string;
  binding: RepositoryBinding;
  onClose: () => void;
}

export function ReviewWorkspaceOverlay({
  intentId,
  binding,
  onClose
}: ReviewWorkspaceOverlayProps) {
  const queryClient = useQueryClient();
  const ws = useReviewWorkspace(intentId, binding.id);
  const { comments, isLoading, error, refresh } = usePullRequestComments(
    intentId,
    binding.id
  );
  const [syncing, setSyncing] = useState(false);

  const handleSync = useCallback(() => {
    setSyncing(true);
    void (async () => {
      try {
        await syncPullRequest(intentId, binding.id);
        void queryClient.invalidateQueries({
          queryKey: intentRepositoriesQueryKeys.list(intentId)
        });
        refresh();
      } finally {
        setSyncing(false);
      }
    })();
  }, [intentId, binding.id, queryClient, refresh]);

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
        onSelectRequest={ws.selectRequestScope}
        onSelectCommit={ws.selectCommit}
        onClose={onClose}
      />
      <div className="flex min-h-0 flex-1">
        <ReviewFilesRail
          files={ws.files}
          activePath={ws.activePath}
          onSelect={ws.selectFile}
          onAdjacent={ws.goToAdjacentFile}
        />
        <main className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
          <DiffRegion
            ws={ws}
            intentId={intentId}
            bindingId={binding.id}
            onSubmitted={handleSync}
          />
        </main>
        <ReviewRightRail
          comments={comments}
          commentsLoading={isLoading}
          commentsError={error}
          syncing={syncing}
          onSync={handleSync}
        />
      </div>
    </div>,
    document.body
  );
}

function DiffRegion({
  ws,
  intentId,
  bindingId,
  onSubmitted
}: {
  ws: ReturnType<typeof useReviewWorkspace>;
  intentId: string;
  bindingId: string;
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
          onSubmitted={onSubmitted}
        />
      </div>
    </>
  );
}
