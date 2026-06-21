import type {
  GitProvider,
  GitPullRequestRef,
  GitRepositoryRef
} from "@/entities/repository-binding";

import { parsePrNumber } from "./pr-number";

/** Where a chip came from: the autocomplete or a hand-pasted SSH URL. */
export type SelectionSource = "search" | "manual";

/** Per-chip lifecycle while binding the batch (see `useBindSelections`). */
export type SelectionStatus = "idle" | "binding" | "error";

export interface RepoSelection {
  /** Stable identity for dedupe + React keys. */
  key: string;
  source: SelectionSource;
  ref: GitRepositoryRef;
  branch: string;
  prNumber: string;
  /** Set only when a PR was picked from the typeahead (search chips). */
  selectedPr: GitPullRequestRef | null;
  status: SelectionStatus;
  /** Server-side error from the last bind attempt of this chip. */
  error: string | null;
}

function defaultHost(provider: GitProvider): string {
  return provider === "github" ? "github.com" : "";
}

export function repoKey(
  provider: GitProvider,
  host: string | null | undefined,
  owner: string,
  repo: string
): string {
  const resolvedHost = (host ?? defaultHost(provider)).toLowerCase();
  return `${provider}|${resolvedHost}|${owner}/${repo}`;
}

export function refKey(ref: GitRepositoryRef): string {
  return repoKey(ref.provider, ref.host, ref.owner, ref.repo);
}

export function createSearchSelection(ref: GitRepositoryRef): RepoSelection {
  return {
    key: refKey(ref),
    source: "search",
    ref,
    branch: ref.default_branch,
    prNumber: "",
    selectedPr: null,
    status: "idle",
    error: null
  };
}

export function createManualSelection(ref: GitRepositoryRef): RepoSelection {
  return {
    key: refKey(ref),
    source: "manual",
    ref,
    branch: "",
    prNumber: "",
    selectedPr: null,
    status: "idle",
    error: null
  };
}

/**
 * Provider/host compatibility for a manually-entered repo. The GitLab clone runs
 * against the configured `Throne:GitLab:Host`, not the coordinate's host, so a
 * mismatch would silently clone the wrong (or no) repo — we reject it up front
 * on the chip instead. `gitlabHost` comes from `git-providers/status`.
 */
export function manualHostError(
  ref: GitRepositoryRef,
  gitlabHost: string | null
): string | null {
  if (ref.provider === "github") {
    return (ref.host ?? "github.com") === "github.com"
      ? null
      : "GitHub доступен только на github.com.";
  }
  if (gitlabHost === null || gitlabHost.trim().length === 0) {
    return "GitLab не настроен. Включите интеграцию в настройках и повторите.";
  }
  return (ref.host ?? "").toLowerCase() === gitlabHost.toLowerCase()
    ? null
    : `Host ${ref.host ?? "?"} не совпадает с настроенным GitLab (${gitlabHost}).`;
}

/**
 * Blocking reason that keeps a chip out of the bind batch. `null` → bindable.
 * Drives both the disabled state of submit and which chips are POSTed.
 */
export function selectionIssue(
  selection: RepoSelection,
  gitlabHost: string | null
): string | null {
  if (selection.source === "manual") {
    const hostError = manualHostError(selection.ref, gitlabHost);
    if (hostError !== null) return hostError;
  }
  if (parsePrNumber(selection.prNumber).kind === "invalid") {
    return "PR number — целое число больше нуля.";
  }
  return null;
}
