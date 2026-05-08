export const ARCHIVE_CONTEXT = "__archive";
export const UNTAGGED_CONTEXT = "__untagged";

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
