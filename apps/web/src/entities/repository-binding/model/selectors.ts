import type { CloneStatus, RepositoryBinding } from "./types";

/**
 * Predicates used by consumers (intent page, settings) to keep render logic
 * declarative and to avoid duplicating status-string checks across UIs.
 */

const TRANSIENT_CLONE_STATUSES: ReadonlySet<CloneStatus> = new Set([
  "pending",
  "cloning"
]);

export function isCloneTransient(status: CloneStatus): boolean {
  return TRANSIENT_CLONE_STATUSES.has(status);
}

export function isCloneReady(status: CloneStatus): boolean {
  return status === "ready";
}

export function isCloneBroken(status: CloneStatus): boolean {
  return status === "failed" || status === "broken";
}

/** True when a manual refresh / PR-comments fetch makes sense for the binding. */
export function hasPullRequest(binding: RepositoryBinding): boolean {
  return typeof binding.pull_request_number === "number";
}

/** `{owner}/{repo}` — convenience label for cards, breadcrumbs, search rows. */
export function repositoryFullName(
  binding: Pick<RepositoryBinding, "owner" | "repo">
): string {
  return `${binding.owner}/${binding.repo}`;
}

/** Sort bindings deterministically: ready first, then by creation time desc. */
export function compareBindings(
  a: RepositoryBinding,
  b: RepositoryBinding
): number {
  const aReady = isCloneReady(a.clone_status) ? 0 : 1;
  const bReady = isCloneReady(b.clone_status) ? 0 : 1;
  if (aReady !== bReady) return aReady - bReady;
  return b.created_at.localeCompare(a.created_at);
}
