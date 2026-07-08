import {
  cardAvailabilityMeta,
  type CardAttachment,
  type CardAvailabilityMeta
} from "./types";

/** Read-only снапшот: содержимое карточки в Throne не редактируется. */
export function isCardEditable(): false {
  return false;
}

/** True, когда карточка была в архиве на момент последнего снапшота. */
export function isCardArchived(card: CardAttachment): boolean {
  return card.archived;
}

export function cardAvailabilityMetaOf(
  card: CardAttachment
): CardAvailabilityMeta {
  return cardAvailabilityMeta[card.availability];
}

/** Координата карточки для заголовка строки: `tracker · board · card`. */
export function cardCoordinateLabel(card: CardAttachment): string {
  return `${card.tracker} · ${card.board_id} · ${card.card_id}`;
}

/**
 * Устойчивый порядок: новые сверху. Снапшоты не «протухают» на чтении, поэтому
 * сортируем по времени привязки, а не по availability.
 */
export function compareCardAttachments(
  a: CardAttachment,
  b: CardAttachment
): number {
  return b.created_at.localeCompare(a.created_at);
}
