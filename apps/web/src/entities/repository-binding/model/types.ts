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
    ink: "#4C5567",
    surface: "#F6F7FB",
    pending: true
  },
  cloning: {
    label: "Клонируем",
    ink: "#3C78F2",
    surface: "#E8F0FF",
    pending: true
  },
  ready: {
    label: "Готово",
    ink: "#1F8F5F",
    surface: "#E7F5ED",
    pending: false
  },
  failed: {
    label: "Ошибка клона",
    ink: "#CF4D4D",
    surface: "#FDEAEA",
    pending: false
  },
  broken: {
    label: "Upstream 404",
    ink: "#B14E1A",
    surface: "#FDE6D6",
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
    ink: "#1F8F5F",
    surface: "#E7F5ED"
  },
  closed: {
    label: "Closed",
    ink: "#CF4D4D",
    surface: "#FDEAEA"
  },
  merged: {
    label: "Merged",
    ink: "#5C49C7",
    surface: "#EEE9FF"
  }
};
