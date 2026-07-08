import type { IntentEvent } from "@/entities/intent-event";

export interface ActivityFeedItem {
  event: IntentEvent;
}

/**
 * Sort the unified feed newest-first for display. Server returns chronological;
 * we render reverse-chronological so the latest activity is at the top.
 */
export function buildActivityFeed(events: IntentEvent[]): ActivityFeedItem[] {
  return [...events]
    .sort((a, b) => b.created_at.localeCompare(a.created_at))
    .map((event) => ({ event }));
}
