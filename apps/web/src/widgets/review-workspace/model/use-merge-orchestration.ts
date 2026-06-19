import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";

import { syncPullRequest } from "@/entities/pull-request-comment";
import { intentRepositoriesQueryKeys } from "@/entities/repository-binding";
import {
  mergePullRequest,
  type MergeStrategy
} from "@/entities/review-workspace";
import { setIntentCleanupOnDone } from "@/features/set-intent-cleanup-on-done";

interface UseMergeOrchestrationArgs {
  intentId: string;
  bindingId: string;
  cleanup: boolean;
  refreshComments: () => void;
}

interface MergeOrchestration {
  syncing: boolean;
  merging: boolean;
  mergeError: string | null;
  mergeNonce: number;
  handleSync: () => void;
  handleMerge: (strategy: MergeStrategy, deleteBranch: boolean) => void;
}

/**
 * Encapsulates the sync / merge dance: sync invalidates the binding list and
 * the comments feed and bumps `mergeNonce` to refetch merge status; merge
 * persists the intent cleanup flag, calls the merge endpoint, then runs sync.
 */
export function useMergeOrchestration({
  intentId,
  bindingId,
  cleanup,
  refreshComments
}: UseMergeOrchestrationArgs): MergeOrchestration {
  const queryClient = useQueryClient();
  const [syncing, setSyncing] = useState(false);
  const [merging, setMerging] = useState(false);
  const [mergeError, setMergeError] = useState<string | null>(null);
  const [mergeNonce, setMergeNonce] = useState(0);

  const handleSync = useCallback(() => {
    setSyncing(true);
    void (async () => {
      try {
        await syncPullRequest(intentId, bindingId);
        void queryClient.invalidateQueries({
          queryKey: intentRepositoriesQueryKeys.list(intentId)
        });
        refreshComments();
        setMergeNonce((n) => n + 1);
      } finally {
        setSyncing(false);
      }
    })();
  }, [intentId, bindingId, queryClient, refreshComments]);

  const handleMerge = useCallback(
    (strategy: MergeStrategy, deleteBranch: boolean) => {
      setMerging(true);
      setMergeError(null);
      void (async () => {
        try {
          await setIntentCleanupOnDone(intentId, cleanup);
          await mergePullRequest(intentId, bindingId, {
            strategy,
            delete_branch: deleteBranch,
            suppress_auto_close: !cleanup
          });
          handleSync();
        } catch (err) {
          setMergeError(err instanceof Error ? err.message : String(err));
        } finally {
          setMerging(false);
        }
      })();
    },
    [intentId, bindingId, cleanup, handleSync]
  );

  return {
    syncing,
    merging,
    mergeError,
    mergeNonce,
    handleSync,
    handleMerge
  };
}
