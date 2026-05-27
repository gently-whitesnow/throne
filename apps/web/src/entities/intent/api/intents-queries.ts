import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { httpGet, intentsEndpoints } from "@/shared/api";

import type {
  IntentAttachment,
  IntentDetail,
  IntentListItem
} from "../model/types";

export const intentsQueryKeys = {
  all: ["intents"] as const,
  lists: () => [...intentsQueryKeys.all, "list"] as const,
  list: () => [...intentsQueryKeys.lists(), "default"] as const,
  details: () => [...intentsQueryKeys.all, "detail"] as const,
  detail: (id: string) => [...intentsQueryKeys.details(), id] as const,
  attachments: (id: string) =>
    [...intentsQueryKeys.detail(id), "attachments"] as const
};

/**
 * Полный плоский список intents текущего пользователя. Виджеты, фильтрующие
 * по context / status, делают это в-памяти поверх этого кеша.
 */
export function useIntents(): UseQueryResult<IntentListItem[]> {
  return useQuery({
    queryKey: intentsQueryKeys.list(),
    queryFn: ({ signal }) =>
      httpGet<IntentListItem[]>(intentsEndpoints.listIntents(), signal)
  });
}

export function useIntent(id: string | null): UseQueryResult<IntentDetail> {
  return useQuery({
    queryKey: id ? intentsQueryKeys.detail(id) : intentsQueryKeys.details(),
    queryFn: ({ signal }) => {
      if (!id) throw new Error("useIntent: id is required");
      return httpGet<IntentDetail>(intentsEndpoints.getIntent(id), signal);
    },
    enabled: id !== null
  });
}

export function useIntentAttachments(
  id: string | null
): UseQueryResult<IntentAttachment[]> {
  return useQuery({
    queryKey: id
      ? intentsQueryKeys.attachments(id)
      : intentsQueryKeys.details(),
    queryFn: ({ signal }) => {
      if (!id) throw new Error("useIntentAttachments: id is required");
      return httpGet<IntentAttachment[]>(
        intentsEndpoints.listIntentAttachments(id),
        signal
      );
    },
    enabled: id !== null
  });
}
