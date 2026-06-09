import { useCallback, useEffect, useMemo, useState } from "react";

import {
  useReviewCommitsQuery,
  useReviewDiffQuery,
  type PullRequestCommit,
  type PullRequestDiff,
  type PullRequestDiffFile,
  type ReviewCommentAnchorShas,
  type ReviewDiffScope
} from "@/entities/review-workspace";

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
  selectRequestScope: () => void;
  selectCommit: (sha: string) => void;
  selectFile: (path: string) => void;
  goToAdjacentFile: (direction: 1 | -1) => void;
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
  initial?: ReviewWorkspaceInitial
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

  const commitsQuery = useReviewCommitsQuery(intentId, bindingId, true);
  const diffQuery = useReviewDiffQuery(
    intentId,
    bindingId,
    scope,
    selectedCommitSha,
    true
  );

  const files = useMemo(
    () => (diffQuery.data ? sortFilesNatural(diffQuery.data.files) : []),
    [diffQuery.data]
  );

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
    selectRequestScope,
    selectCommit,
    selectFile,
    goToAdjacentFile
  };
}
