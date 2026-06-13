export type {
  ReviewDiffScope,
  ReviewCommentSide,
  PullRequestDiff,
  PullRequestDiffFile,
  ReviewFileLines,
  ReviewFileLine,
  PullRequestDiffFileStatus,
  PullRequestCommit,
  PullRequestHeader,
  SubmitReviewCommentRequest,
  SubmittedReviewComment,
  ReviewCommentAnchorShas,
  MergeStrategy,
  PullRequestMergeability,
  PullRequestChecksState,
  PullRequestMergeStatus,
  MergePullRequestRequest,
  MergePullRequestResult
} from "./model/types";

export {
  getReviewDiff,
  getReviewFileLines,
  getReviewPullRequest,
  listReviewCommits,
  submitReviewComment
} from "./api/review-api";

export { getPullRequestMergeStatus, mergePullRequest } from "./api/merge-api";

export {
  reviewWorkspaceQueryKeys,
  useReviewDiffQuery,
  useReviewPullRequestQuery,
  useReviewCommitsQuery
} from "./api/review-queries";

export {
  parseUnifiedDiff,
  countPatchChanges,
  type DiffRow,
  type DiffRowKind,
  type DiffHunk,
  type ChangeCounts
} from "./lib/parse-unified-diff";

export {
  fileLinesToContextRows,
  findDiffGaps,
  mergeFullFileLinesWithDiff,
  type DiffGap
} from "./lib/diff-context";

export {
  loadDraft,
  saveDraft,
  clearDraft,
  type DraftAnchor
} from "./lib/comment-drafts";

export { detectLanguage, highlightLine } from "./lib/highlight-line";
