import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { LocalModelCatalog } from "../model/types";
import { fetchLocalModelCatalog } from "./local-model-catalog-api";

export const localModelCatalogQueryKeys = {
  all: ["local-model-catalog"] as const,
  current: () => [...localModelCatalogQueryKeys.all, "current"] as const
};

export function useLocalModelCatalogQuery(): UseQueryResult<LocalModelCatalog> {
  return useQuery({
    queryKey: localModelCatalogQueryKeys.current(),
    queryFn: ({ signal }) => fetchLocalModelCatalog(signal)
  });
}
