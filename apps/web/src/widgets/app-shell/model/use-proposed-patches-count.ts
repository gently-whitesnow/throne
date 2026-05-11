import { useCallback, useEffect, useState } from "react";

import type { InstructionPatchesComponents } from "@/shared/api";
import { httpGet, instructionPatchesEndpoints } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";

type InstructionPatchPage =
  InstructionPatchesComponents["schemas"]["InstructionPatchPageDto"];

/**
 * Sidebar counter for the /improvements badge — counts InstructionPatches
 * currently in `proposed` status owned by the caller. Refreshes on each
 * relevant realtime fanout so the badge does not drift.
 */
export function useProposedPatchesCount(): number {
  const [count, setCount] = useState<number>(0);

  const refresh = useCallback(() => {
    const controller = new AbortController();
    const url = `${instructionPatchesEndpoints.listInstructionPatches()}?status=proposed&limit=200`;
    httpGet<InstructionPatchPage>(url, controller.signal)
      .then((page) => {
        setCount(page.items.length);
      })
      .catch(() => {
        // оставляем последнее известное значение, без шума в shell
      });
    return () => {
      controller.abort();
    };
  }, []);

  useEffect(() => refresh(), [refresh]);

  useRealtimeEvent("instruction_patch.proposed", refresh);
  useRealtimeEvent("instruction_patch.applied", refresh);
  useRealtimeEvent("instruction_patch.rejected", refresh);
  useRealtimeEvent("instruction_patch.superseded", refresh);

  return count;
}
