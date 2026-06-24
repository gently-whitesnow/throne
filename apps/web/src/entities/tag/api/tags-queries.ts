import {
  useInfiniteQuery,
  useQuery,
  type UseInfiniteQueryResult,
  type UseQueryResult
} from "@tanstack/react-query";

import type { TagsComponents } from "@/shared/api";

import type { TagDetail, TagListItem } from "../model/types";
import {
  fetchTag,
  fetchTagsPage,
  type TagListParams
} from "./tags-api";

type TagListPage = TagsComponents["schemas"]["TagListPageDto"];

const TAGS_STALE_TIME_MS = 5 * 60_000;
const TYPEAHEAD_STALE_TIME_MS = 60_000;

export const tagsQueryKeys = {
  all: ["tags"] as const,
  lists: () => [...tagsQueryKeys.all, "list"] as const,
  list: (search?: string) => [...tagsQueryKeys.lists(), search ?? ""] as const,
  typeaheads: () => [...tagsQueryKeys.all, "typeahead"] as const,
  typeahead: (search: string, limit: number) =>
    [...tagsQueryKeys.typeaheads(), search, limit] as const,
  detail: (id: string) => [...tagsQueryKeys.all, "detail", id] as const
};

/**
 * Курсорно-пагинированный список тегов (последняя привязка desc, id asc).
 * Поиск и пагинация считаются на сервере. Виджеты сами решают, когда делать
 * `fetchNextPage` (скролл-сентинел в борде).
 */
export function useInfiniteTags(
  params: TagListParams = {}
): UseInfiniteQueryResult<{
  pages: TagListPage[];
  pageParams: (string | undefined)[];
}> {
  const trimmed = params.search?.trim();
  const search =
    trimmed !== undefined && trimmed.length > 0 ? trimmed : undefined;
  return useInfiniteQuery({
    queryKey: tagsQueryKeys.list(search),
    queryFn: ({ pageParam, signal }) =>
      fetchTagsPage({ search, limit: params.limit }, pageParam, signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.next_cursor ?? undefined,
    staleTime: TAGS_STALE_TIME_MS
  });
}

export interface UseTagsTypeaheadResult {
  data: TagListItem[];
  isFetching: boolean;
  isError: boolean;
  error: unknown;
}

/**
 * Первая страница серверной выдачи под typeahead-пикер: substring-поиск по
 * имени + сортировка по последней привязке живут на сервере, отдельный
 * typeahead-эндпоинт не заводим. Пустой `search` отдаёт самые недавно
 * использованные теги.
 */
export function useTagsTypeahead(
  search: string,
  limit: number
): UseTagsTypeaheadResult {
  const trimmed = search.trim();
  const effective = trimmed.length > 0 ? trimmed : undefined;
  const query = useQuery({
    queryKey: tagsQueryKeys.typeahead(trimmed, limit),
    queryFn: ({ signal }) =>
      fetchTagsPage({ search: effective, limit }, undefined, signal),
    staleTime: TYPEAHEAD_STALE_TIME_MS,
    placeholderData: (prev) => prev
  });
  return {
    data: query.data?.items ?? [],
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error
  };
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
