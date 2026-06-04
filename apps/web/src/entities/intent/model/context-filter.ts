import {
  ARCHIVE_CONTEXT,
  FRIDGE_CONTEXT,
  INBOX_HELP_CONTEXT,
  INBOX_REVIEW_CONTEXT,
  PINNED_CONTEXT,
  TERMINAL_RUNNING_CONTEXT,
  UNTAGGED_CONTEXT,
  archiveContextTag,
  isArchiveContext,
  isFridgeContext,
  isInboxContext,
  isPinnedContext,
  isTerminalRunningContext
} from "@/shared/lib";

import type { IntentListParams } from "../api/intents-queries";

import {
  ARCHIVE_STATUSES,
  FRIDGE_STATUS,
  type IntentListItem,
  type IntentStatus
} from "./types";

const ACTIVE_STATUSES: IntentStatus[] = [
  "draft",
  "interview",
  "ready_for_work",
  "work",
  "ready_for_review",
  "needs_help"
];

const ARCHIVE_STATUS_LIST: IntentStatus[] = ["done", "reject"];

/**
 * Does this intent belong to the supplied context bucket? Mirrors the rules
 * applied by the rail when it counts contexts, so list and canvas surfaces
 * see the same subset.
 */
export function matchesContext(
  item: IntentListItem,
  context: string | null
): boolean {
  if (!context) return false;
  // Membership in the terminal-running bucket lives in tmux, not on the item — the list is
  // fetched server-side via the `terminal_running` filter, so there is nothing to mirror here.
  if (isTerminalRunningContext(context)) return false;
  if (isPinnedContext(context)) {
    return item.pinned_in.length > 0;
  }
  if (isArchiveContext(context)) {
    if (!ARCHIVE_STATUSES.has(item.status)) return false;
    const subTag = archiveContextTag(context);
    if (subTag === null) return true;
    if (subTag === UNTAGGED_CONTEXT) return item.tags.length === 0;
    return item.tags.some((t) => t.name === subTag);
  }
  if (isFridgeContext(context)) {
    return item.status === FRIDGE_STATUS;
  }
  if (isInboxContext(context)) {
    if (context === INBOX_REVIEW_CONTEXT)
      return item.status === "ready_for_review";
    if (context === INBOX_HELP_CONTEXT) return item.status === "needs_help";
    return false;
  }
  if (ARCHIVE_STATUSES.has(item.status)) return false;
  if (item.status === FRIDGE_STATUS) return false;
  if (context === UNTAGGED_CONTEXT) {
    return item.tags.length === 0;
  }
  return item.tags.some((t) => t.name === context);
}

/**
 * Translate a context bucket into server-side filter params for
 * `/api/v1/intents`. Every bucket is now fully expressible server-side
 * (status / tag / untagged / pinned), so callers fetch only the matching
 * subset instead of pulling the whole list and filtering in memory.
 */
export function contextToParams(context: string | null): IntentListParams {
  if (!context) return {};
  if (isTerminalRunningContext(context)) return { terminalRunning: true };
  if (isPinnedContext(context)) return { pinned: true };
  if (isArchiveContext(context)) {
    const subTag = archiveContextTag(context);
    if (subTag === null) return { status: ARCHIVE_STATUS_LIST };
    if (subTag === UNTAGGED_CONTEXT)
      return { status: ARCHIVE_STATUS_LIST, untagged: true };
    return { status: ARCHIVE_STATUS_LIST, tag: subTag };
  }
  if (isFridgeContext(context)) return { status: [FRIDGE_STATUS] };
  if (isInboxContext(context)) {
    if (context === INBOX_REVIEW_CONTEXT)
      return { status: ["ready_for_review"] };
    if (context === INBOX_HELP_CONTEXT) return { status: ["needs_help"] };
    return {};
  }
  if (context === UNTAGGED_CONTEXT)
    return { status: ACTIVE_STATUSES, untagged: true };
  return { status: ACTIVE_STATUSES, tag: context };
}

/** Human-readable label of the supplied context bucket. */
export function contextTitle(context: string | null): string {
  if (!context) return "Intents";
  if (isArchiveContext(context)) {
    const subTag = archiveContextTag(context);
    if (subTag === null) return "Архив";
    if (subTag === UNTAGGED_CONTEXT) return "Архив · Без тегов";
    return `Архив · # ${subTag}`;
  }
  if (context === FRIDGE_CONTEXT) return "Холодильник";
  if (context === INBOX_REVIEW_CONTEXT) return "Жду ревью";
  if (context === INBOX_HELP_CONTEXT) return "Нужна помощь";
  if (context === TERMINAL_RUNNING_CONTEXT) return "Терминал запущен";
  if (context === UNTAGGED_CONTEXT) return "Без тегов";
  if (context === PINNED_CONTEXT) return "Pinned";
  if (context === ARCHIVE_CONTEXT) return "Архив";
  return `# ${context}`;
}
