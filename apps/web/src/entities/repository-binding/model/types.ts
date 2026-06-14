import type { RepositoriesComponents } from "@/shared/api";

export type RepositoryBinding =
  RepositoriesComponents["schemas"]["RepositoryBindingDto"];

export type RepositoryBindingSummary =
  RepositoriesComponents["schemas"]["RepositoryBindingSummary"];

export type GitRepositoryRef =
  RepositoriesComponents["schemas"]["GitRepositoryRefDto"];

export type GitBranchRef = RepositoriesComponents["schemas"]["GitBranchRefDto"];

export type GitPullRequestRef =
  RepositoriesComponents["schemas"]["GitPullRequestRefDto"];

export type BindRepositoryRequest =
  RepositoriesComponents["schemas"]["BindIntentRepositoryRequest"];

export type CloneStatus = RepositoriesComponents["schemas"]["CloneStatus"];

export type PullRequestState =
  RepositoriesComponents["schemas"]["PullRequestState"];

export type GitProvider = RepositoriesComponents["schemas"]["GitProvider"];

export type RepositorySearchScope =
  RepositoriesComponents["schemas"]["RepositorySearchScope"];

export interface CloneStatusMeta {
  label: string;
  ink: string;
  surface: string;
  /** Transient state — render spinner / progress affordance. */
  pending: boolean;
}

/** Light-first semantic palette for the binding lifecycle (see entities/intent). */
export const cloneStatusMeta: Record<CloneStatus, CloneStatusMeta> = {
  pending: {
    label: "В очереди",
    ink: "var(--color-status-neutral-ink)",
    surface: "var(--color-status-neutral-surface)",
    pending: true
  },
  cloning: {
    label: "Клонируем",
    ink: "var(--color-status-info-ink)",
    surface: "var(--color-status-info-surface)",
    pending: true
  },
  ready: {
    label: "Готово",
    ink: "var(--color-status-success-ink)",
    surface: "var(--color-status-success-surface)",
    pending: false
  },
  failed: {
    label: "Ошибка клона",
    ink: "var(--color-status-danger-ink)",
    surface: "var(--color-status-danger-surface)",
    pending: false
  },
  broken: {
    label: "Upstream 404",
    ink: "var(--color-status-attention-ink)",
    surface: "var(--color-status-attention-surface)",
    pending: false
  }
};

export interface PullRequestStateMeta {
  label: string;
  ink: string;
  surface: string;
}

export const pullRequestStateMeta: Record<
  PullRequestState,
  PullRequestStateMeta
> = {
  open: {
    label: "Open",
    ink: "var(--color-status-success-ink)",
    surface: "var(--color-status-success-surface)"
  },
  closed: {
    label: "Closed",
    ink: "var(--color-status-danger-ink)",
    surface: "var(--color-status-danger-surface)"
  },
  merged: {
    label: "Merged",
    ink: "var(--color-status-merged-ink)",
    surface: "var(--color-status-merged-surface)"
  }
};
