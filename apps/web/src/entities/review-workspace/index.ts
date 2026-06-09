export type {
  ReviewDiffScope,
  ReviewCommentSide,
  PullRequestDiff,
  PullRequestDiffFile,
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
  loadDraft,
  saveDraft,
  clearDraft,
  type DraftAnchor
} from "./lib/comment-drafts";

export { detectLanguage, highlightLine } from "./lib/highlight-line";
