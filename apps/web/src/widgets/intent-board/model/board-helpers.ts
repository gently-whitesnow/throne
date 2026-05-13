import {
  ARCHIVE_STATUSES,
  FRIDGE_STATUS,
  type IntentListItem
} from "@/entities/intent";
import {
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
  return `# ${context}`;
}

export function emptyMessage(context: string | null, total: number): string {
  if (!context) {
    return total === 0
      ? "Нет ни одного intent. Создайте первый."
      : "Выберите контекст слева.";
  }
  if (isArchiveContext(context)) return "В архиве пусто.";
  if (context === FRIDGE_CONTEXT)
    return "В холодильнике пусто — отложите сюда intents «на потом».";
  if (context === INBOX_REVIEW_CONTEXT)
    return "Нет intents, ждущих ревью. Хороший момент.";
  if (context === INBOX_HELP_CONTEXT)
    return "Нет intents, где агент просит помощи.";
  if (context === UNTAGGED_CONTEXT) {
    return "Все active intents уже разнесены по тегам.";
  }
  if (context === PINNED_CONTEXT) {
    return "Нет запиненных intents. Перетащите карточку в секцию «Pinned».";
  }
  return "В этом контексте пока пусто.";
}

export function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}

/**
 * Build a placeholder sort_key for an optimistic reorder. The server is the
 * source of truth — this just keeps the dragged row visually between its
 * neighbours until the realtime intent.reordered event arrives with the
 * canonical key.
 */
export function synthesizeSortKey(
  items: readonly IntentListItem[],
  beforeId: string | null,
  afterId: string | null
): string {
  const lookup = (id: string | null) =>
    id ? (items.find((i) => i.id === id)?.sort_key ?? null) : null;
  const before = lookup(beforeId);
  const after = lookup(afterId);
  if (before && after) return midpoint(before, after);
  if (before) return before + "V";
  if (after) return "0" + after;
  return "V";
}

function midpoint(a: string, b: string): string {
  // Cheap stand-in: server replaces the key on response, so we just need
  // string ordering between the two neighbours.
  if (a < b) return a + "V";
  return b + "V";
}
