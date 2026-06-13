import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";

import {
  promptPartPatchesQueryKeys,
  type PromptPartPatch,
  type PromptPartPatchStatus,
  usePromptPartPatchesList
} from "@/entities/prompt-part-patch";

export interface UsePromptPartPatchesOptions {
  status?: PromptPartPatchStatus;
  targetScope?: string;
  targetKey?: string;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: PromptPartPatch[]; nextCursor?: string }
  | { kind: "error"; message: string };

export function usePromptPartPatches(opts: UsePromptPartPatchesOptions = {}): {
  state: LoadState;
  reload: () => void;
} {
  const qc = useQueryClient();
  const query = usePromptPartPatchesList({
    status: opts.status,
    targetScope: opts.targetScope,
    targetKey: opts.targetKey,
    limit: 50
  });

  const reload = useCallback(() => {
    void qc.invalidateQueries({
      queryKey: promptPartPatchesQueryKeys.all
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
