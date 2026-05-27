import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { httpGet, instructionsEndpoints } from "@/shared/api";

import type { BundlesTreeData } from "./types";

export const bundlesTreeQueryKeys = {
  all: ["bundles-tree"] as const,
  current: () => [...bundlesTreeQueryKeys.all, "current"] as const
};

export function useBundlesTreeQuery(): UseQueryResult<BundlesTreeData> {
  return useQuery({
    queryKey: bundlesTreeQueryKeys.current(),
    queryFn: ({ signal }) =>
      httpGet<BundlesTreeData>(instructionsEndpoints.getBundlesTree(), signal)
  });
}
