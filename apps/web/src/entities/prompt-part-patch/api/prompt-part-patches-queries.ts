import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { PromptPartPatchPage } from "../model/types";
import {
  listPromptPartPatches,
  type ListPromptPartPatchesQuery
} from "./list-patches";

export const promptPartPatchesQueryKeys = {
  all: ["prompt-part-patches"] as const,
  lists: () => [...promptPartPatchesQueryKeys.all, "list"] as const,
  list: (params: ListPromptPartPatchesQuery) =>
    [...promptPartPatchesQueryKeys.lists(), params] as const,
  proposedCount: () =>
    [...promptPartPatchesQueryKeys.all, "proposed-count"] as const
};

export function usePromptPartPatchesList(
  params: ListPromptPartPatchesQuery
): UseQueryResult<PromptPartPatchPage> {
  return useQuery({
    queryKey: promptPartPatchesQueryKeys.list(params),
    queryFn: ({ signal }) => listPromptPartPatches(params, signal)
  });
}

export function useProposedPromptPartPatchesCount(): UseQueryResult<number> {
  return useQuery({
    queryKey: promptPartPatchesQueryKeys.proposedCount(),
    queryFn: ({ signal }) =>
      listPromptPartPatches({ status: "proposed", limit: 200 }, signal).then(
        (page) => page.items.length
      )
  });
}
