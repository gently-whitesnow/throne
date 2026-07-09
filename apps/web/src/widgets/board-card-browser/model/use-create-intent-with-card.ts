import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import type { IntentDetail } from "@/entities/intent";
import {
  attachIntentCard,
  cardAttachmentsQueryKeys,
  type TaskTrackerCard
} from "@/entities/task-tracker-card";
import { httpPost, intentsEndpoints } from "@/shared/api";
import { errorMessage } from "@/shared/lib";

interface UseCreateIntentWithCard {
  createWithCard: (card: TaskTrackerCard) => Promise<void>;
  pending: boolean;
  error: string | null;
}

/**
 * Materialises a board card into a fresh intent: create (title from the card,
 * empty body) → attach the card as a read-only snapshot → navigate to the new
 * intent, preserving the current querystring. The attach step is non-fatal: if
 * the tracker is offline (502) we still land on the created intent and surface
 * the error, so the operator never loses their intent.
 */
export function useCreateIntentWithCard(
  tracker: string
): UseCreateIntentWithCard {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const queryClient = useQueryClient();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const createWithCard = async (card: TaskTrackerCard): Promise<void> => {
    if (pending) return;
    setPending(true);
    setError(null);

    // Intent.Create rejects an empty body, so seed the text from the card TITLE
    // (with an id fallback for a blank title). The description is deliberately
    // NOT copied — it stays read-only context on the attachment (ADR-0052).
    const seed =
      card.title.trim().length > 0 ? card.title : `Карточка ${card.card_id}`;

    let created: IntentDetail;
    try {
      created = await httpPost<IntentDetail>(intentsEndpoints.createIntent(), {
        title: card.title,
        text: seed
      });
    } catch (err: unknown) {
      setError(errorMessage(err, { base: "Не удалось создать интент" }));
      setPending(false);
      return;
    }

    try {
      await attachIntentCard(created.id, {
        tracker,
        board_id: card.board_id,
        card_id: card.card_id
      });
      void queryClient.invalidateQueries({
        queryKey: cardAttachmentsQueryKeys.list(created.id)
      });
    } catch (err: unknown) {
      setError(
        errorMessage(err, {
          base: "Интент создан, но карточку не удалось привязать"
        })
      );
    }

    const search = params.toString();
    const target =
      search.length > 0
        ? `/intents/${created.id}?${search}`
        : `/intents/${created.id}`;
    void navigate(target);
    setPending(false);
  };

  return { createWithCard, pending, error };
}
