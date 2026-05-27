import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { DreamSessionPage, DreamSourcePage } from "../model/types";
import {
  listDreamSessions,
  type ListDreamSessionsQuery
} from "./list-sessions";
import { listDreamSources } from "./list-sources";

const DREAM_SOURCES_STALE_TIME_MS = 5 * 60_000;

export const dreamsQueryKeys = {
  all: ["dreams"] as const,
  sessionLists: () => [...dreamsQueryKeys.all, "sessions"] as const,
  sessionsList: (params: ListDreamSessionsQuery) =>
    [...dreamsQueryKeys.sessionLists(), params] as const,
  sourcesList: () => [...dreamsQueryKeys.all, "sources"] as const
};

export function useDreamSessionsList(
  params: ListDreamSessionsQuery
): UseQueryResult<DreamSessionPage> {
  return useQuery({
    queryKey: dreamsQueryKeys.sessionsList(params),
    queryFn: ({ signal }) => listDreamSessions(params, signal)
  });
}

export function useDreamSourcesList(): UseQueryResult<DreamSourcePage> {
  return useQuery({
    queryKey: dreamsQueryKeys.sourcesList(),
    queryFn: ({ signal }) => listDreamSources(signal),
    staleTime: DREAM_SOURCES_STALE_TIME_MS
  });
}
