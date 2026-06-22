import {
  useQueries,
  useQuery,
  type UseQueryResult
} from "@tanstack/react-query";

import type { Tag, TagDetail, TagUsage } from "../model/types";
import { fetchTag, fetchTags, fetchTagUsage } from "./tags-api";

const TAGS_STALE_TIME_MS = 5 * 60_000;

export const tagsQueryKeys = {
  all: ["tags"] as const,
  list: () => [...tagsQueryKeys.all, "list"] as const,
  detail: (id: string) => [...tagsQueryKeys.all, "detail", id] as const,
  usage: (id: string) => [...tagsQueryKeys.all, "usage", id] as const
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

/**
 * Число привязанных интентов для каждого тега. Bulk-эндпоинта нет, поэтому
 * дёргаем per-tag `GET /tags/{id}/usage` через useQueries с тем же ключом, что и
 * `useTagUsage` — модалка удаления переиспользует кеш без повторного запроса.
 * Числа меняются редко: staleTime 5 минут, инвалидируются realtime'ом.
 */
export function useTagUsages(ids: readonly string[]): Map<string, number> {
  const results = useQueries({
    queries: ids.map((id) => ({
      queryKey: tagsQueryKeys.usage(id),
      queryFn: ({ signal }) => fetchTagUsage(id, signal),
      staleTime: TAGS_STALE_TIME_MS
    }))
  });
  const counts = new Map<string, number>();
  results.forEach((result, index) => {
    if (result.data) {
      counts.set(ids[index], result.data.intents_count);
    }
  });
  return counts;
}

/**
 * Usage одного тега для модалки удаления. Делит ключ с `useTagUsages`, поэтому
 * при открытии модалки запрос обычно уже в кеше.
 */
export function useTagUsage(id: string | null): UseQueryResult<TagUsage> {
  return useQuery({
    queryKey: tagsQueryKeys.usage(id ?? ""),
    queryFn: ({ signal }) => fetchTagUsage(id ?? "", signal),
    enabled: id !== null && id.length > 0,
    staleTime: TAGS_STALE_TIME_MS
  });
}
