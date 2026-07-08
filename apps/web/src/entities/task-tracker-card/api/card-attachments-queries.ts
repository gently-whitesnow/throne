import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import { compareCardAttachments } from "../model/selectors";
import type { AttachCardRequest, CardAttachment } from "../model/types";
import {
  attachIntentCard,
  detachIntentCard,
  listIntentCardAttachments,
  refreshIntentCard
} from "./card-attachments-api";

export const cardAttachmentsQueryKeys = {
  all: ["intent-card-attachments"] as const,
  list: (intentId: string) =>
    [...cardAttachmentsQueryKeys.all, intentId] as const
};

/**
 * Список приложенных карточек интента. Снапшот не переопрашивается на чтении —
 * список освежается только через invalidation на мутациях attach/detach/refresh
 * (realtime-подписки у карточек нет, в отличие от `intent.repository_*`).
 */
export function useIntentCardAttachmentsQuery(
  intentId: string
): UseQueryResult<CardAttachment[]> {
  return useQuery({
    queryKey: cardAttachmentsQueryKeys.list(intentId),
    queryFn: ({ signal }) => listIntentCardAttachments(intentId, signal),
    select: (data) => [...data].sort(compareCardAttachments)
  });
}

export function useAttachCardMutation(
  intentId: string
): UseMutationResult<CardAttachment, Error, AttachCardRequest> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AttachCardRequest) => attachIntentCard(intentId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: cardAttachmentsQueryKeys.list(intentId)
      });
    }
  });
}

export function useDetachCardMutation(
  intentId: string
): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachmentId: string) =>
      detachIntentCard(intentId, attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: cardAttachmentsQueryKeys.list(intentId)
      });
    }
  });
}

export function useRefreshCardMutation(
  intentId: string
): UseMutationResult<CardAttachment, Error, string> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachmentId: string) =>
      refreshIntentCard(intentId, attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: cardAttachmentsQueryKeys.list(intentId)
      });
    }
  });
}
