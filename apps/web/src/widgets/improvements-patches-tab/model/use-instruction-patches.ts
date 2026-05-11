import { useCallback, useEffect, useState } from "react";

import {
  type InstructionPatch,
  type InstructionPatchPage,
  type InstructionPatchStatus,
  type InstructionPatchTargetKind,
  listInstructionPatches
} from "@/entities/instruction-patch";
import { useRealtimeEvent } from "@/shared/realtime";

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
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    listInstructionPatches(
      { status: opts.status, targetKind: opts.targetKind, limit: 50 },
      controller.signal
    )
      .then((page: InstructionPatchPage) => {
        setState({
          kind: "ready",
          items: page.items,
          nextCursor: page.next_cursor
        });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof Error
              ? err.message
              : "Не удалось загрузить InstructionPatches."
        });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey, opts.status, opts.targetKind]);

  const reload = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  useRealtimeEvent("instruction_patch.proposed", reload);
  useRealtimeEvent("instruction_patch.applied", reload);
  useRealtimeEvent("instruction_patch.rejected", reload);
  useRealtimeEvent("instruction_patch.superseded", reload);
  // A new DreamSession typically lands together with a batch of fresh
  // proposed patches (ADR-0022); refresh the list so the UI surfaces them
  // without waiting for the next manual reload.
  useRealtimeEvent("dream_session.recorded", reload);

  return { state, reload };
}
