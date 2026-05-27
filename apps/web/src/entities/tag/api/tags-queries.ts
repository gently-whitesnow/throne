import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { Tag } from "../model/types";
import { fetchTags } from "./tags-api";

const TAGS_STALE_TIME_MS = 5 * 60_000;

export const tagsQueryKeys = {
  all: ["tags"] as const,
  list: () => [...tagsQueryKeys.all, "list"] as const
};

/**
 * Список всех тегов рабочего пространства. Теги меняются редко — staleTime
 * поднят до 5 минут, реальные мутации приходят realtime'ом и инвалидируют ключ
 * через RealtimeQueryBridge.
 */
export function useTags(): UseQueryResult<Tag[]> {
  return useQuery({
    queryKey: tagsQueryKeys.list(),
    queryFn: ({ signal }) => fetchTags(signal),
    staleTime: TAGS_STALE_TIME_MS
  });
}
