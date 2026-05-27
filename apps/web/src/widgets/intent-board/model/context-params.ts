import type { IntentListParams, IntentStatus } from "@/entities/intent";
import {
  ARCHIVE_CONTEXT,
  INBOX_HELP_CONTEXT,
  INBOX_REVIEW_CONTEXT,
  UNTAGGED_CONTEXT,
  archiveContextTag,
  isArchiveContext,
  isFridgeContext,
  isInboxContext,
  isPinnedContext
} from "@/shared/lib";

const ACTIVE_STATUSES: IntentStatus[] = [
  "draft",
  "interview",
  "ready_for_work",
  "work",
  "ready_for_review",
  "needs_help"
];

const ARCHIVE_STATUSES: IntentStatus[] = ["done", "reject"];

/**
 * Translate a board context bucket into server-side filter params for
 * /api/v1/intents. Where the bucket can't be expressed by status+tag alone
 * (PINNED — needs any pin entry; UNTAGGED — needs an empty tag list) we
 * leave it as a client-side post-filter on the pages we fetch.
 */
export function contextToParams(context: string | null): IntentListParams {
  if (!context) return {};
  if (isPinnedContext(context)) return {};
  if (isArchiveContext(context)) {
    if (context === ARCHIVE_CONTEXT) return { status: ARCHIVE_STATUSES };
    const subTag = archiveContextTag(context);
    if (subTag === null) return { status: ARCHIVE_STATUSES };
    if (subTag === UNTAGGED_CONTEXT) return { status: ARCHIVE_STATUSES };
    return { status: ARCHIVE_STATUSES, tag: subTag };
  }
  if (isFridgeContext(context)) return { status: ["fridge"] };
  if (isInboxContext(context)) {
    if (context === INBOX_REVIEW_CONTEXT)
      return { status: ["ready_for_review"] };
    if (context === INBOX_HELP_CONTEXT) return { status: ["needs_help"] };
    return {};
  }
  if (context === UNTAGGED_CONTEXT) return { status: ACTIVE_STATUSES };
  return { status: ACTIVE_STATUSES, tag: context };
}

/**
 * True if items fetched under {@link contextToParams} still need a
 * client-side post-filter (untagged / pinned / archive-untagged).
 */
export function needsClientPostFilter(context: string | null): boolean {
  if (!context) return false;
  if (isPinnedContext(context)) return true;
  if (context === UNTAGGED_CONTEXT) return true;
  if (isArchiveContext(context)) {
    const subTag = archiveContextTag(context);
    return subTag === UNTAGGED_CONTEXT;
  }
  return false;
}
