import type { IntentsComponents } from "@/shared/api";

export type IntentLinkView = IntentsComponents["schemas"]["IntentLinkViewDto"];
export type IntentLinkDirection =
  IntentsComponents["schemas"]["IntentLinkDirection"];

export type DisplayBucket =
  | "blocking_incoming"
  | "blocking_outgoing"
  | "normal_incoming"
  | "normal_outgoing";

export const BUCKET_ORDER: DisplayBucket[] = [
  "blocking_incoming",
  "blocking_outgoing",
  "normal_incoming",
  "normal_outgoing"
];

export const bucketLabel: Record<DisplayBucket, string> = {
  blocking_incoming: "Блокируется",
  blocking_outgoing: "Блокирует",
  normal_incoming: "Вытекает из",
  normal_outgoing: "Ведёт к"
};

export function bucketOf(view: IntentLinkView): DisplayBucket {
  const incoming = view.direction === "incoming";
  if (view.link.blocking) {
    return incoming ? "blocking_incoming" : "blocking_outgoing";
  }
  return incoming ? "normal_incoming" : "normal_outgoing";
}

export interface BucketDropParams {
  fromId: string;
  toId: string;
  blocking: boolean;
}

export function bucketDropParams(
  bucket: DisplayBucket,
  currentId: string,
  peerId: string
): BucketDropParams {
  switch (bucket) {
    case "blocking_outgoing":
      return { fromId: currentId, toId: peerId, blocking: true };
    case "blocking_incoming":
      return { fromId: peerId, toId: currentId, blocking: true };
    case "normal_outgoing":
      return { fromId: currentId, toId: peerId, blocking: false };
    case "normal_incoming":
      return { fromId: peerId, toId: currentId, blocking: false };
  }
}
