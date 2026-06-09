import type { RepositoriesComponents } from "@/shared/api";

export type ReviewDiffScope =
  RepositoriesComponents["schemas"]["ReviewDiffScope"];

export type ReviewCommentSide =
  RepositoriesComponents["schemas"]["ReviewCommentSide"];

export type PullRequestDiff =
  RepositoriesComponents["schemas"]["PullRequestDiffDto"];

export type PullRequestDiffFile =
  RepositoriesComponents["schemas"]["PullRequestDiffFileDto"];

export type PullRequestDiffFileStatus =
  RepositoriesComponents["schemas"]["PullRequestDiffFileStatus"];

export type PullRequestCommit =
  RepositoriesComponents["schemas"]["PullRequestCommitDto"];

export type SubmitReviewCommentRequest =
  RepositoriesComponents["schemas"]["SubmitReviewCommentRequest"];

export type SubmittedReviewComment =
  RepositoriesComponents["schemas"]["SubmittedReviewCommentDto"];

export type PullRequestHeader =
  RepositoriesComponents["schemas"]["PullRequestHeaderDto"];

/** Anchor части submit-контракта, которые UI берёт из diff-ответа as-is. */
export type ReviewCommentAnchorShas = Pick<
  SubmitReviewCommentRequest,
  "commit_sha" | "base_sha" | "start_sha"
>;

export type MergeStrategy = RepositoriesComponents["schemas"]["MergeStrategy"];

export type PullRequestMergeability =
  RepositoriesComponents["schemas"]["PullRequestMergeability"];

export type PullRequestChecksState =
  RepositoriesComponents["schemas"]["PullRequestChecksState"];

export type PullRequestMergeStatus =
  RepositoriesComponents["schemas"]["PullRequestMergeStatusDto"];

export type MergePullRequestRequest =
  RepositoriesComponents["schemas"]["MergePullRequestRequest"];

export type MergePullRequestResult =
  RepositoriesComponents["schemas"]["MergePullRequestResultDto"];
