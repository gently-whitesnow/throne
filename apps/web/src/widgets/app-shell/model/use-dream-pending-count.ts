import { useCallback, useEffect, useState } from "react";

import type { DreamPendingCount } from "@/entities/dream-run";
import { dreamEndpoints, httpGet } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";

export function useDreamPendingCount(): number {
  const [count, setCount] = useState<number>(0);

  const refresh = useCallback(() => {
    const controller = new AbortController();
    httpGet<DreamPendingCount>(
      dreamEndpoints.getPendingDreamProposalsCount(),
      controller.signal
    )
      .then((data) => {
        setCount(data.pending_proposals_count);
      })
      .catch(() => {
        // оставляем последнее известное значение, без шума в shell
      });
    return () => {
      controller.abort();
    };
  }, []);

  useEffect(() => refresh(), [refresh]);

  useRealtimeEvent("dream.proposal_created", refresh);
  useRealtimeEvent("dream.proposal_applied", refresh);
  useRealtimeEvent("dream.proposal_skipped", refresh);
  useRealtimeEvent("dream.run_closed", refresh);

  return count;
}
