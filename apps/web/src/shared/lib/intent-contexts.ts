export const ARCHIVE_CONTEXT = "__archive";
export const UNTAGGED_CONTEXT = "__untagged";
export const FRIDGE_CONTEXT = "__fridge";
export const INBOX_REVIEW_CONTEXT = "__inbox_review";
export const INBOX_HELP_CONTEXT = "__inbox_help";

const ARCHIVE_PREFIX = `${ARCHIVE_CONTEXT}:`;

export function isArchiveContext(context: string | null): boolean {
  if (!context) return false;
  return context === ARCHIVE_CONTEXT || context.startsWith(ARCHIVE_PREFIX);
}

export function archiveSubContext(tagOrUntagged: string): string {
  return `${ARCHIVE_PREFIX}${tagOrUntagged}`;
}

export function archiveContextTag(context: string | null): string | null {
  if (!context?.startsWith(ARCHIVE_PREFIX)) return null;
  return context.slice(ARCHIVE_PREFIX.length);
}

export function isFridgeContext(context: string | null): boolean {
  return context === FRIDGE_CONTEXT;
}

export function isInboxContext(context: string | null): boolean {
  return context === INBOX_REVIEW_CONTEXT || context === INBOX_HELP_CONTEXT;
}
