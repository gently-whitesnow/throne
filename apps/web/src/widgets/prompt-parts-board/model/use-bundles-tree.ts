import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { httpGet, instructionsEndpoints } from "@/shared/api";
import type { InstructionsComponents } from "@/shared/api";

export type BundlesTreeData =
  InstructionsComponents["schemas"]["BundlesTreeDto"];
export type BundleNode = InstructionsComponents["schemas"]["BundleNodeDto"];
export type BundleEntryNode =
  InstructionsComponents["schemas"]["BundleEntryNodeDto"];

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
