import type { IntentQa } from "@/entities/intent-qa";
import type { IntentReview } from "@/entities/intent-review";
import type { TextVersion } from "@/entities/text-version";

export type ActivityEvent =
  | { kind: "version"; at: string; version: TextVersion }
  | { kind: "qa"; at: string; qa: IntentQa }
  | { kind: "review"; at: string; review: IntentReview };

export function buildActivityFeed(
  versions: TextVersion[],
  qa: IntentQa[],
  reviews: IntentReview[]
): ActivityEvent[] {
  const events: ActivityEvent[] = [
    ...versions.map<ActivityEvent>((v) => ({
      kind: "version",
      at: v.changed_at,
      version: v
    })),
    ...qa.map<ActivityEvent>((q) => ({
      kind: "qa",
      at: q.created_at,
      qa: q
    })),
    ...reviews.map<ActivityEvent>((r) => ({
      kind: "review",
      at: r.created_at,
      review: r
    }))
  ];
  // Newest first; stable secondary sort by event-kind so versions outrank
  // qa/review when timestamps tie (versions get bumped first).
  events.sort((a, b) => {
    const cmp = b.at.localeCompare(a.at);
    if (cmp !== 0) return cmp;
    return rank(a.kind) - rank(b.kind);
  });
  return events;
}

function rank(kind: ActivityEvent["kind"]): number {
  switch (kind) {
    case "version":
      return 0;
    case "qa":
      return 1;
    case "review":
      return 2;
  }
}
