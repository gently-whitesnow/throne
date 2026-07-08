import { useState } from "react";

import {
  useDetachCardMutation,
  useRefreshCardMutation,
  type CardAttachment
} from "@/entities/task-tracker-card";
import { errorMessage } from "@/shared/lib";

interface UseCardRowActionsArgs {
  intentId: string;
  card: CardAttachment;
  label: string;
}

/**
 * Per-row действия строки карточки: «Обновить» (online-only ре-pull снапшота) и
 * «Отвязать» (detach). Список освежается через query-invalidation внутри мутаций,
 * поэтому хук держит только loading/error. Refresh ошибок не даёт (бэкенд при
 * недоступности сохраняет прошлый снапшот и деградирует availability), но сетевой
 * сбой самого запроса всё равно ловим. Detach идемпотентен (204).
 */
export function useCardRowActions({
  intentId,
  card,
  label
}: UseCardRowActionsArgs) {
  const refreshMutation = useRefreshCardMutation(intentId);
  const detachMutation = useDetachCardMutation(intentId);
  const [error, setError] = useState<string | null>(null);

  const refreshing = refreshMutation.isPending;
  const detaching = detachMutation.isPending;
  const busy = refreshing || detaching;

  async function refresh() {
    setError(null);
    try {
      await refreshMutation.mutateAsync(card.id);
    } catch (err) {
      setError(
        errorMessage(err, {
          base: "Не удалось обновить карточку",
          fallback: "Не удалось обновить карточку."
        })
      );
    }
  }

  async function detach() {
    const confirmed = window.confirm(
      `Отвязать карточку ${label} от интента?\n\n` +
        "Снапшот перестанет отображаться в панели. Содержимое карточки в трекере " +
        "не меняется — Throne никогда не пишет в неё.\n\nОтвязать?"
    );
    if (!confirmed) return;
    setError(null);
    try {
      await detachMutation.mutateAsync(card.id);
    } catch (err) {
      setError(
        errorMessage(err, {
          base: "Не удалось отвязать карточку",
          fallback: "Не удалось отвязать карточку."
        })
      );
    }
  }

  return { refresh, detach, refreshing, detaching, busy, error };
}
