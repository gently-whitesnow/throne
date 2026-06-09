import type { ReviewCommentSide } from "../model/types";

/**
 * Черновики inline-комментариев живут только в localStorage до отправки —
 * серверного durable-стора в MVP нет (Slice 4). Ключ привязан к binding + файлу
 * + якорю, чтобы черновик переживал переключение файлов и перезагрузку вкладки,
 * но не протекал между разными комментариями.
 */

const PREFIX = "throne.review-draft";

export interface DraftAnchor {
  bindingId: string;
  path: string;
  side: ReviewCommentSide;
  line: number;
}

function draftKey(anchor: DraftAnchor): string {
  return `${PREFIX}:${anchor.bindingId}:${anchor.path}:${anchor.side}:${String(
    anchor.line
  )}`;
}

function safeStorage(): Storage | null {
  try {
    return typeof window !== "undefined" ? window.localStorage : null;
  } catch {
    return null;
  }
}

export function loadDraft(anchor: DraftAnchor): string {
  const store = safeStorage();
  if (store === null) return "";
  return store.getItem(draftKey(anchor)) ?? "";
}

export function saveDraft(anchor: DraftAnchor, value: string): void {
  const store = safeStorage();
  if (store === null) return;
  const key = draftKey(anchor);
  if (value.trim().length === 0) {
    store.removeItem(key);
    return;
  }
  store.setItem(key, value);
}

export function clearDraft(anchor: DraftAnchor): void {
  const store = safeStorage();
  if (store === null) return;
  store.removeItem(draftKey(anchor));
}
