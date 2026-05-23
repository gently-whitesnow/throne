import { useCallback, useEffect, useState } from "react";

import { useRealtimeEvent } from "@/shared/realtime";

import { listPullRequestComments } from "../api/pr-comments-api";
import { compareComments } from "./types";
import type { PullRequestComment } from "./types";

export interface PullRequestCommentsState {
  comments: PullRequestComment[];
  isLoading: boolean;
  error: Error | null;
  refresh: () => void;
}

const EMPTY: PullRequestComment[] = [];

/**
 * Loads + tail-subscribes to the PR review comments feed for one binding.
 *
 * Initial load reads the full feed (slice 1 is unpaginated). Subsequent
 * `intent.pr_comment_added` events insert new comments in place — no
 * full refetch, and the realtime payload is also fanned by manual sync
 * so this hook stays consistent regardless of refresh trigger.
 */
export function usePullRequestComments(
  intentId: string | null,
  bindingId: string | null
): PullRequestCommentsState {
  const [comments, setComments] = useState<PullRequestComment[]>(EMPTY);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (intentId === null || bindingId === null) {
      setComments(EMPTY);
      setIsLoading(false);
      setError(null);
      return;
    }

    const controller = new AbortController();
    setIsLoading(true);
    setError(null);

    listPullRequestComments(intentId, bindingId, undefined, controller.signal)
      .then((list) => {
        setComments([...list].sort(compareComments));
        setIsLoading(false);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setError(err instanceof Error ? err : new Error(String(err)));
        setIsLoading(false);
      });

    return () => {
      controller.abort();
    };
  }, [intentId, bindingId, reloadKey]);

  const onAdded = useCallback(
    (payload: { intent_id: string; binding_id: string; comment: unknown }) => {
      if (
        intentId === null ||
        bindingId === null ||
        payload.intent_id !== intentId ||
        payload.binding_id !== bindingId
      ) {
        return;
      }
      // Generated event payload types `comment` as `unknown` for inline $ref
      // properties — the runtime value is always PullRequestCommentDto.
      const next = payload.comment as PullRequestComment;
      setComments((prev) => {
        const without = prev.filter((c) => c.id !== next.id);
        return [...without, next].sort(compareComments);
      });
    },
    [intentId, bindingId]
  );
  // prettier-ignore
  useRealtimeEvent("intent.pr_comment_added", onAdded);

  const refresh = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  return { comments, isLoading, error, refresh };
}
