import {
  ARCHIVE_CONTEXT,
  FRIDGE_CONTEXT,
  INBOX_HELP_CONTEXT,
  INBOX_REVIEW_CONTEXT,
  PINNED_CONTEXT,
  UNTAGGED_CONTEXT,
  archiveContextTag,
  isArchiveContext,
  isFridgeContext,
  isInboxContext,
  isPinnedContext
} from "@/shared/lib";

import { ARCHIVE_STATUSES, FRIDGE_STATUS, type IntentListItem } from "./types";

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
  if (context === UNTAGGED_CONTEXT) return "Без тегов";
  if (context === PINNED_CONTEXT) return "Pinned";
  if (context === ARCHIVE_CONTEXT) return "Архив";
  return `# ${context}`;
}
