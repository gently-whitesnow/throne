import {
  useInfiniteQuery,
  useQuery,
  type UseInfiniteQueryResult,
  type UseQueryResult
} from "@tanstack/react-query";
import { useEffect, useMemo } from "react";

import type { TagsComponents } from "@/shared/api";

import type { TagDetail, TagListItem, TagUsage } from "../model/types";
import {
  fetchTag,
  fetchTagsPage,
  fetchTagUsage,
  type TagListParams
} from "./tags-api";

type TagListPage = TagsComponents["schemas"]["TagListPageDto"];

const TAGS_STALE_TIME_MS = 5 * 60_000;

export const tagsQueryKeys = {
  all: ["tags"] as const,
  lists: () => [...tagsQueryKeys.all, "list"] as const,
  list: (search?: string) => [...tagsQueryKeys.lists(), search ?? ""] as const,
  detail: (id: string) => [...tagsQueryKeys.all, "detail", id] as const,
  usage: (id: string) => [...tagsQueryKeys.all, "usage", id] as const
};

/**
 * Курсорно-пагинированный список тегов (сортировка `usage desc, id asc`).
 * Поиск и пагинация считаются на сервере — `intents_count` едет инлайн в каждом
 * item'е, отдельного per-tag usage-запроса нет. Виджеты сами решают, когда
 * `fetchNextPage` (скролл-сентинел в борде).
 */
export function useInfiniteTags(
  params: TagListParams = {}
): UseInfiniteQueryResult<{
  pages: TagListPage[];
  pageParams: (string | undefined)[];
}> {
  const trimmed = params.search?.trim();
  const search = trimmed !== undefined && trimmed.length > 0 ? trimmed : undefined;
  return useInfiniteQuery({
    queryKey: tagsQueryKeys.list(search),
    queryFn: ({ pageParam, signal }) =>
      fetchTagsPage({ search, limit: params.limit }, pageParam, signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.next_cursor ?? undefined,
    staleTime: TAGS_STALE_TIME_MS
  });
}

export interface UseAllTagsResult {
  data: TagListItem[] | undefined;
  isError: boolean;
  error: unknown;
}

/**
 * Плоский фасад поверх курсорной пагинации: подсасывает все страницы и отдаёт
 * `TagListItem[]`. Для потребителей, которым нужна полная картина (пикер тегов
 * с клиентским автокомплитом). Борд использует `useInfiniteTags` напрямую.
 */
export function useAllTags(): UseAllTagsResult {
  const query = useInfiniteTags();
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = query;

  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      void fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, query.data]);

  const items = useMemo(
    () => query.data?.pages.flatMap((p) => p.items),
    [query.data]
  );

  return { data: items, isError: query.isError, error: query.error };
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
 * Usage одного тега для модалки удаления. На борде число едет инлайн в item'е,
 * но диалог открывается из любого места и подтягивает свежий счёт точечно.
 */
export function useTagUsage(id: string | null): UseQueryResult<TagUsage> {
  return useQuery({
    queryKey: tagsQueryKeys.usage(id ?? ""),
    queryFn: ({ signal }) => fetchTagUsage(id ?? "", signal),
    enabled: id !== null && id.length > 0,
    staleTime: TAGS_STALE_TIME_MS
  });
}
