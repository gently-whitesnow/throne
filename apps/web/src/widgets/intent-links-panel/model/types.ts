import type { IntentsComponents } from "@/shared/api";

export type IntentLinkView = IntentsComponents["schemas"]["IntentLinkViewDto"];
export type IntentLinkType = IntentsComponents["schemas"]["IntentLinkType"];
export type IntentLinkDirection =
  IntentsComponents["schemas"]["IntentLinkDirection"];

export type DisplayBucket =
  | "relates"
  | "blocks_outgoing"
  | "blocks_incoming"
  | "derived_outgoing"
  | "derived_incoming";

export const bucketLabel: Record<DisplayBucket, string> = {
  relates: "Связано",
  blocks_outgoing: "Блокирует",
  blocks_incoming: "Блокируется",
  derived_outgoing: "Происходит из",
  derived_incoming: "Источник для"
};

/**
 * Map a link view to its display bucket. Mirror roles (`blocked_by`, `source_of`) are
 * incoming projections, never separate edges (ADR-0018) — we render them in their own
 * bucket so the user reads the relationship from their own perspective.
 */
export function bucketOf(view: IntentLinkView): DisplayBucket {
  const t = view.link.type;
  const incoming = view.direction === "incoming";
  if (t === "blocks") {
    return incoming ? "blocks_incoming" : "blocks_outgoing";
  }
  if (t === "derived_from") {
    return incoming ? "derived_incoming" : "derived_outgoing";
  }
  // `relates` is symmetric — bucket independently of direction.
  return "relates";
}
