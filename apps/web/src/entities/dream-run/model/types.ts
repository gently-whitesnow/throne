import type { DreamComponents } from "@/shared/api";

export type DreamRun = DreamComponents["schemas"]["DreamRunDto"];
export type DreamRunDetail = DreamComponents["schemas"]["DreamRunDetailDto"];
export type DreamIntentRef = DreamComponents["schemas"]["DreamIntentRefDto"];
export type DreamPendingCount =
  DreamComponents["schemas"]["DreamPendingCountDto"];

export function pendingProposalsCount(run: DreamRun): number {
  return run.proposals.filter((p) => p.decision === "pending").length;
}

export function isEmptyRun(run: DreamRun): boolean {
  return run.proposals.length === 0;
}
