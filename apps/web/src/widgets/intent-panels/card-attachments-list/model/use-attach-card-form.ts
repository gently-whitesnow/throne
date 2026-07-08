import { useMemo, useState } from "react";

import { useAttachCardMutation } from "@/entities/task-tracker-card";
import {
  useTaskTrackerBoardSelectionQuery,
  useTaskTrackerConnectionsQuery
} from "@/entities/task-tracker";
import { errorMessage } from "@/shared/lib";

/**
 * Состояние минимальной формы attach: трекер + доска из board-selection
 * (`/settings`) дают координату `tracker`+`board_id`, оператор вводит только
 * `card_id`. Типизированные ошибки бэкенда (404/409/422/502) переводятся в
 * человекочитаемый текст; поиск карточек по тексту отложен (Child 5).
 */
export function useAttachCardForm(intentId: string, onAttached: () => void) {
  const connectionsQuery = useTaskTrackerConnectionsQuery();
  const connectedTrackers = useMemo(
    () =>
      (connectionsQuery.data?.connections ?? []).filter(
        (connection) => connection.state === "connected"
      ),
    [connectionsQuery.data]
  );

  const [tracker, setTracker] = useState<string>("");
  const [boardId, setBoardId] = useState<string>("");
  const [cardId, setCardId] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const activeTracker = tracker || connectedTrackers[0]?.tracker || "";
  const boardsQuery = useTaskTrackerBoardSelectionQuery(
    activeTracker,
    activeTracker.length > 0
  );
  const boards = boardsQuery.data?.boards ?? [];
  const activeBoardId = boardId || boards[0]?.board_id || "";

  const attachMutation = useAttachCardMutation(intentId);
  const canSubmit =
    activeTracker.length > 0 &&
    activeBoardId.length > 0 &&
    cardId.trim().length > 0 &&
    !attachMutation.isPending;

  async function submit() {
    if (!canSubmit) return;
    setError(null);
    try {
      await attachMutation.mutateAsync({
        tracker: activeTracker,
        board_id: activeBoardId,
        card_id: cardId.trim()
      });
      setCardId("");
      onAttached();
    } catch (err) {
      setError(
        errorMessage(err, {
          base: "Не удалось приложить карточку",
          byStatus: {
            404: "Карточка или интент не найдены — проверьте id карточки.",
            409: "Трекер не подключён — подключите его в настройках.",
            422: "Трекер не поддержан или координата карточки некорректна.",
            502: "Трекер недоступен — повторите позже."
          },
          fallback: "Не удалось приложить карточку."
        })
      );
    }
  }

  return {
    connectionsLoading: connectionsQuery.isLoading,
    connectedTrackers,
    tracker: activeTracker,
    setTracker: (value: string) => {
      setTracker(value);
      setBoardId("");
    },
    boards,
    boardsLoading: boardsQuery.isLoading,
    boardId: activeBoardId,
    setBoardId,
    cardId,
    setCardId,
    submit,
    submitting: attachMutation.isPending,
    canSubmit,
    error
  };
}
