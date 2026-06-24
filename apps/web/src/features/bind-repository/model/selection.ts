import {
  manualHostError,
  refKey,
  type GitPullRequestRef,
  type GitRepositoryRef,
  type PickerSelection
} from "@/entities/repository-binding";

import { parsePrNumber } from "./pr-number";

export type SelectionSource = PickerSelection["source"];

/** Per-chip lifecycle while binding the batch (см. `useBindSelections`). */
export type SelectionStatus = "idle" | "binding" | "error";

export interface RepoSelection extends PickerSelection {
  branch: string;
  prNumber: string;
  /** Set only when a PR was picked from the typeahead (search chips). */
  selectedPr: GitPullRequestRef | null;
  status: SelectionStatus;
  /** Server-side error from the last bind attempt of this chip. */
  error: string | null;
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
