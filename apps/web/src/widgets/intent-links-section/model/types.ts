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

export const BUCKET_ORDER: DisplayBucket[] = [
  "blocks_incoming",
  "blocks_outgoing",
  "derived_outgoing",
  "derived_incoming",
  "relates"
];

export const bucketLabel: Record<DisplayBucket, string> = {
  relates: "Связано",
  blocks_outgoing: "Блокирует",
  blocks_incoming: "Блокируется",
  derived_outgoing: "Происходит из",
  derived_incoming: "Источник для"
};

/**
 * Map a link view to its display bucket. Mirror roles (`blocked_by`, `source_of`)
 * are incoming projections, never separate edges (ADR-0018) — мы рендерим их в
 * своём бакете так, чтобы юзер читал отношение со своей стороны.
 */
export function bucketOf(view: IntentLinkView): DisplayBucket {
  const t = view.link.type;
  const incoming = view.direction === "incoming";
  if (t === "blocks") return incoming ? "blocks_incoming" : "blocks_outgoing";
  if (t === "derived_from")
    return incoming ? "derived_incoming" : "derived_outgoing";
  return "relates";
}

/**
 * Параметры запроса POST /intents/{from}/links для добавления связи указанного
 * типа в указанный бакет с указанным peer. Учитывает, что incoming-бакеты
 * требуют обратной направленности (peer → current).
 */
export interface BucketDropParams {
  fromId: string;
  toId: string;
  type: IntentLinkType;
}

export function bucketDropParams(
  bucket: DisplayBucket,
  currentId: string,
  peerId: string
): BucketDropParams {
  switch (bucket) {
    case "blocks_outgoing":
      return { fromId: currentId, toId: peerId, type: "blocks" };
    case "blocks_incoming":
      return { fromId: peerId, toId: currentId, type: "blocks" };
    case "derived_outgoing":
      return { fromId: currentId, toId: peerId, type: "derived_from" };
    case "derived_incoming":
      return { fromId: peerId, toId: currentId, type: "derived_from" };
    case "relates":
      return { fromId: currentId, toId: peerId, type: "relates" };
  }
}
