import { useMemo } from "react";

import { useIntentContexts } from "@/entities/intent";

export interface ContextRow {
  key: string;
  label: string;
  count: number;
  icon: "tag" | "untagged" | "archive";
}

type ContextsData = ReturnType<typeof useIntentContexts>["data"];

export function useContextRows(data: ContextsData) {
  return useMemo(() => {
    const counts = data;
    // Server already orders tag breakdowns by count desc then name asc.
    const toRows = (
      rows: readonly { tag: string; count: number }[]
    ): ContextRow[] =>
      rows.map((row) => ({
        key: row.tag,
        label: row.tag,
        count: row.count,
        icon: "tag" as const
      }));
    return {
      tagRows: counts ? toRows(counts.tags) : [],
      untaggedCount: counts?.untagged ?? 0,
      archiveCount: counts?.archive ?? 0,
      archiveTagRows: counts ? toRows(counts.archive_tags) : [],
      archiveUntaggedCount: counts?.archive_untagged ?? 0,
      fridgeCount: counts?.fridge ?? 0,
      inboxReviewCount: counts?.inbox_review ?? 0,
      inboxHelpCount: counts?.inbox_help ?? 0,
      terminalRunningCount: counts?.terminal_running ?? 0
    };
  }, [data]);
}
