import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";

import {
  instructionPatchesQueryKeys,
  type InstructionPatch,
  type InstructionPatchStatus,
  type InstructionPatchTargetKind,
  useInstructionPatchesList
} from "@/entities/instruction-patch";

export interface UseInstructionPatchesOptions {
  status?: InstructionPatchStatus;
  targetKind?: InstructionPatchTargetKind;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: InstructionPatch[]; nextCursor?: string }
  | { kind: "error"; message: string };

export function useInstructionPatches(
  opts: UseInstructionPatchesOptions = {}
): {
  state: LoadState;
  reload: () => void;
} {
  const qc = useQueryClient();
  const query = useInstructionPatchesList({
    status: opts.status,
    targetKind: opts.targetKind,
    limit: 50
  });

  const reload = useCallback(() => {
    void qc.invalidateQueries({
      queryKey: instructionPatchesQueryKeys.all
    });
  }, [qc]);

  const state: LoadState = query.isPending
    ? { kind: "loading" }
    : query.error
      ? { kind: "error", message: query.error.message }
      : {
          kind: "ready",
          items: query.data.items,
          nextCursor: query.data.next_cursor
        };

  return { state, reload };
}
