import type { IntentLinksSummaryEntry } from "@/entities/intent";

/**
 * Derive structural parents of an intent from its link summary:
 *   • outgoing `derived_from` — peers this intent was structurally derived from
 *   • incoming `blocks` (a.k.a. `blocked_by`) — peers that block this intent
 *
 * Returned ids are de-duplicated and filtered to the supplied `visible` set,
 * so cross-context edges drop out of the canvas DAG (mirrors the board, which
 * never renders peers it can't show).
 */
export function parentsFromSummary(
  summary: IntentLinksSummaryEntry | undefined,
  visible: ReadonlySet<string>,
  selfId: string
): string[] {
  if (!summary) return [];
  const out: string[] = [];
  const seen = new Set<string>();
  const push = (id: string): void => {
    if (id === selfId) return;
    if (seen.has(id)) return;
    if (!visible.has(id)) return;
    seen.add(id);
    out.push(id);
  };
  for (const p of summary.derived_from) push(p.id);
  for (const p of summary.blocked_by) push(p.id);
  return out;
}
