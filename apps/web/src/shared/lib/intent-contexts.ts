export const ARCHIVE_CONTEXT = "__archive";
export const UNTAGGED_CONTEXT = "__untagged";
export const FRIDGE_CONTEXT = "__fridge";
export const INBOX_HELP_CONTEXT = "__inbox_help";
export const TERMINAL_RUNNING_CONTEXT = "__terminal_running";
export const PINNED_CONTEXT = "__pinned";
export const BOARD_CONTEXT = "__board";

const ARCHIVE_PREFIX = `${ARCHIVE_CONTEXT}:`;
const FRIDGE_PREFIX = `${FRIDGE_CONTEXT}:`;
const BOARD_PREFIX = `${BOARD_CONTEXT}:`;

export function isPinnedContext(context: string | null): boolean {
  return context === PINNED_CONTEXT;
}

/**
 * Returns true when the supplied context represents a real tag (i.e. neither a
 * built-in virtual context nor an archive sub-context). Pinning only makes
 * sense in tag-scoped contexts — virtual buckets cannot become pin owners.
 */
export function isTagContext(context: string | null): boolean {
  if (!context) return false;
  if (context.startsWith("__")) return false;
  if (isArchiveContext(context)) return false;
  return true;
}

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
  if (!context) return false;
  return context === FRIDGE_CONTEXT || context.startsWith(FRIDGE_PREFIX);
}

export function fridgeSubContext(tagOrUntagged: string): string {
  return `${FRIDGE_PREFIX}${tagOrUntagged}`;
}

export function fridgeContextTag(context: string | null): string | null {
  if (!context?.startsWith(FRIDGE_PREFIX)) return null;
  return context.slice(FRIDGE_PREFIX.length);
}

/**
 * Task-tracker board group. The key carries both the provider key and the board id
 * (`__board:<tracker>:<boardId>`) so the rail can both filter the list (by board id) and
 * address the board for a force-pull (which needs the tracker too).
 */
export function isBoardContext(context: string | null): boolean {
  if (!context) return false;
  return context.startsWith(BOARD_PREFIX);
}

export function boardContext(tracker: string, boardId: string): string {
  return `${BOARD_PREFIX}${tracker}:${boardId}`;
}

export function boardContextParts(
  context: string | null
): { tracker: string; boardId: string } | null {
  if (!context?.startsWith(BOARD_PREFIX)) return null;
  const rest = context.slice(BOARD_PREFIX.length);
  const idx = rest.indexOf(":");
  if (idx <= 0 || idx >= rest.length - 1) return null;
  return { tracker: rest.slice(0, idx), boardId: rest.slice(idx + 1) };
}

export function isInboxContext(context: string | null): boolean {
  return context === INBOX_HELP_CONTEXT;
}

/**
 * Cross-context virtual bucket: intents whose embedded-terminal tmux session is alive.
 * Membership is server-derived (tmux is the source of truth), so it cannot be computed
 * from a list item — only fetched via the `terminal_running` filter.
 */
export function isTerminalRunningContext(context: string | null): boolean {
  return context === TERMINAL_RUNNING_CONTEXT;
}
