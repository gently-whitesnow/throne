import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { httpGet, promptPartsEndpoints } from "@/shared/api";
import type { PromptPartsComponents } from "@/shared/api";

export type BundlesTreeData =
  PromptPartsComponents["schemas"]["BundlesTreeDto"];
export type BundleNode = PromptPartsComponents["schemas"]["BundleNodeDto"];
export type BundleEntryNode =
  PromptPartsComponents["schemas"]["BundleEntryNodeDto"];

export const bundlesTreeQueryKeys = {
  all: ["bundles-tree"] as const,
  current: () => [...bundlesTreeQueryKeys.all, "current"] as const
};

export function useBundlesTreeQuery(): UseQueryResult<BundlesTreeData> {
  return useQuery({
    queryKey: bundlesTreeQueryKeys.current(),
    queryFn: ({ signal }) =>
      httpGet<BundlesTreeData>(promptPartsEndpoints.getBundlesTree(), signal)
  });
}
