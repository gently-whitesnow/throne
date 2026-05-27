import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { Tag, TagDetail } from "../model/types";
import { fetchTag, fetchTags } from "./tags-api";

const TAGS_STALE_TIME_MS = 5 * 60_000;

export const tagsQueryKeys = {
  all: ["tags"] as const,
  list: () => [...tagsQueryKeys.all, "list"] as const,
  detail: (id: string) => [...tagsQueryKeys.all, "detail", id] as const
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

/**
 * Полное состояние одного тега, включая `default_repositories[]` — используется
 * страницей `/tags` для секции «Default repositories» (Slice 2).
 */
export function useTag(id: string | null): UseQueryResult<TagDetail> {
  return useQuery({
    queryKey: tagsQueryKeys.detail(id ?? ""),
    queryFn: ({ signal }) => fetchTag(id ?? "", signal),
    enabled: id !== null && id.length > 0,
    staleTime: TAGS_STALE_TIME_MS
  });
}
