import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { compareBindings } from "../model/selectors";
import type { RepositoryBinding } from "../model/types";
import { listIntentRepositories } from "./repository-bindings-api";

export const intentRepositoriesQueryKeys = {
  all: ["intent-repositories"] as const,
  list: (intentId: string) =>
    [...intentRepositoriesQueryKeys.all, intentId] as const
};

export function useIntentRepositoriesQuery(
  intentId: string | null
): UseQueryResult<RepositoryBinding[]> {
  return useQuery({
    queryKey:
      intentId !== null
        ? intentRepositoriesQueryKeys.list(intentId)
        : intentRepositoriesQueryKeys.all,
    queryFn: ({ signal }) => {
      if (intentId === null) {
        throw new Error("useIntentRepositoriesQuery: intentId is required");
      }
      return listIntentRepositories(intentId, signal);
    },
    enabled: intentId !== null,
    select: (data) => [...data].sort(compareBindings)
  });
}
