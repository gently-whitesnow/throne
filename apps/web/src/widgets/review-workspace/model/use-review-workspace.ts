import { useCallback, useEffect, useMemo, useState } from "react";

import type { ReviewFileOrderEntry } from "@/entities/pull-request-artifact";
import {
  useReviewCommitsQuery,
  useReviewDiffQuery,
  type PullRequestCommit,
  type PullRequestDiff,
  type PullRequestDiffFile,
  type ReviewCommentAnchorShas,
  type ReviewCommentSide,
  type ReviewDiffScope
} from "@/entities/review-workspace";

import { orderFilesByAi } from "../lib/order-files-by-ai";

/** Transient request to scroll a specific diff row into view and flash it. */
export interface ScrollTarget {
  path: string;
  side: ReviewCommentSide;
  line: number;
  /** Bumped on every request so the viewer re-runs even for the same row. */
  nonce: number;
}

export interface ReviewWorkspaceState {
  scope: ReviewDiffScope;
  selectedCommitSha: string | null;
  commits: PullRequestCommit[];
  commitsLoading: boolean;
  diff: PullRequestDiff | undefined;
  diffLoading: boolean;
  diffError: Error | null;
  files: PullRequestDiffFile[];
  activePath: string | null;
  activeFile: PullRequestDiffFile | null;
  anchorShas: ReviewCommentAnchorShas | null;
  scrollTarget: ScrollTarget | null;
  selectRequestScope: () => void;
  selectCommit: (sha: string) => void;
  selectFile: (path: string) => void;
  goToAdjacentFile: (direction: 1 | -1) => void;
  /** Switch to `path` (if needed) and request the matching row to scroll/flash. */
  jumpToComment: (
    path: string,
    side: ReviewCommentSide | null,
    line: number | null
  ) => void;
}

/** Базовая сортировка files rail — `natural` по пути (Slice 4B scope). */
function sortFilesNatural(files: PullRequestDiffFile[]): PullRequestDiffFile[] {
  return [...files].sort((a, b) =>
    a.path.localeCompare(b.path, undefined, { numeric: true })
  );
}

export interface ReviewWorkspaceInitial {
  scope?: ReviewDiffScope;
  commitSha?: string | null;
  path?: string | null;
}

export function useReviewWorkspace(
  intentId: string,
  bindingId: string,
  initial?: ReviewWorkspaceInitial,
  aiFileOrder?: ReviewFileOrderEntry[] | null
): ReviewWorkspaceState {
  const [scope, setScope] = useState<ReviewDiffScope>(
    initial?.scope ?? "request"
  );
  const [selectedCommitSha, setSelectedCommitSha] = useState<string | null>(
    initial?.commitSha ?? null
  );
  const [activePath, setActivePath] = useState<string | null>(
    initial?.path ?? null
  );
  const [scrollTarget, setScrollTarget] = useState<ScrollTarget | null>(null);

  const commitsQuery = useReviewCommitsQuery(intentId, bindingId, true);
  const diffQuery = useReviewDiffQuery(
    intentId,
    bindingId,
    scope,
    selectedCommitSha,
    true
  );

  const files = useMemo(() => {
    if (!diffQuery.data) return [];
    const natural = sortFilesNatural(diffQuery.data.files);
    if (
      aiFileOrder === undefined ||
      aiFileOrder === null ||
      aiFileOrder.length === 0
    ) {
      return natural;
    }
    return orderFilesByAi(natural, aiFileOrder).files;
  }, [diffQuery.data, aiFileOrder]);

  // Держим выбранный файл валидным: на новом diff'е без выбора берём первый.
  useEffect(() => {
    if (files.length === 0) {
      setActivePath(null);
      return;
    }
    setActivePath((prev) =>
      prev !== null && files.some((f) => f.path === prev) ? prev : files[0].path
    );
  }, [files]);

  const selectRequestScope = useCallback(() => {
    setScope("request");
    setSelectedCommitSha(null);
    setActivePath(null);
  }, []);

  const selectCommit = useCallback((sha: string) => {
    setScope("commit");
    setSelectedCommitSha(sha);
    setActivePath(null);
  }, []);

  const selectFile = useCallback((path: string) => {
    setActivePath(path);
  }, []);

  const jumpToComment = useCallback(
    (path: string, side: ReviewCommentSide | null, line: number | null) => {
      setActivePath(path);
      setScrollTarget(
        side !== null && line !== null
          ? { path, side, line, nonce: Date.now() }
          : null
      );
    },
    []
  );

  const goToAdjacentFile = useCallback(
    (direction: 1 | -1) => {
      setActivePath((prev) => {
        if (files.length === 0) return prev;
        const index = files.findIndex((f) => f.path === prev);
        const nextIndex = Math.min(
          Math.max((index === -1 ? 0 : index) + direction, 0),
          files.length - 1
        );
        return files[nextIndex].path;
      });
    },
    [files]
  );

  const activeFile =
    files.find((f) => f.path === activePath) ??
    (files.length > 0 ? files[0] : null);

  const anchorShas: ReviewCommentAnchorShas | null = diffQuery.data
    ? {
        commit_sha: diffQuery.data.head_sha,
        base_sha: diffQuery.data.base_sha,
        start_sha: diffQuery.data.start_sha
      }
    : null;

  return {
    scope,
    selectedCommitSha,
    commits: commitsQuery.data ?? [],
    commitsLoading: commitsQuery.isLoading,
    diff: diffQuery.data,
    diffLoading: diffQuery.isLoading,
    diffError: diffQuery.error,
    files,
    activePath,
    activeFile,
    anchorShas,
    scrollTarget,
    selectRequestScope,
    selectCommit,
    selectFile,
    goToAdjacentFile,
    jumpToComment
  };
}
