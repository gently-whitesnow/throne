import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { httpGet, intentsEndpoints } from "@/shared/api";

import type { IntentEvent } from "../model/types";

export const intentEventsQueryKeys = {
  all: ["intent-events"] as const,
  list: (intentId: string) =>
    [...intentEventsQueryKeys.all, "list", intentId] as const
};

export function useIntentEvents(
  intentId: string | null
): UseQueryResult<IntentEvent[]> {
  return useQuery({
    queryKey: intentId
      ? intentEventsQueryKeys.list(intentId)
      : intentEventsQueryKeys.all,
    queryFn: ({ signal }) => {
      if (!intentId) throw new Error("useIntentEvents: intentId is required");
      return httpGet<IntentEvent[]>(
        intentsEndpoints.listIntentEvents(intentId),
        signal
      );
    },
    enabled: intentId !== null
  });
}
