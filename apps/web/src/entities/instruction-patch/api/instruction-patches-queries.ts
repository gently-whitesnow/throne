import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { InstructionPatchPage } from "../model/types";
import {
  listInstructionPatches,
  type ListInstructionPatchesQuery
} from "./list-patches";

export const instructionPatchesQueryKeys = {
  all: ["instruction-patches"] as const,
  lists: () => [...instructionPatchesQueryKeys.all, "list"] as const,
  list: (params: ListInstructionPatchesQuery) =>
    [...instructionPatchesQueryKeys.lists(), params] as const,
  proposedCount: () =>
    [...instructionPatchesQueryKeys.all, "proposed-count"] as const
};

export function useInstructionPatchesList(
  params: ListInstructionPatchesQuery
): UseQueryResult<InstructionPatchPage> {
  return useQuery({
    queryKey: instructionPatchesQueryKeys.list(params),
    queryFn: ({ signal }) => listInstructionPatches(params, signal)
  });
}

export function useProposedInstructionPatchesCount(): UseQueryResult<number> {
  return useQuery({
    queryKey: instructionPatchesQueryKeys.proposedCount(),
    queryFn: ({ signal }) =>
      listInstructionPatches({ status: "proposed", limit: 200 }, signal).then(
        (page) => page.items.length
      )
  });
}
