import { useEffect } from "react";

import { realtimeClient } from "./client";
import type { RealtimeEventMap, RealtimeEventName } from "./generated/events";

/**
 * Subscribe a React component to a realtime event for the lifetime of its mount.
 * The handler ref is captured per render — wrap with useCallback or pass a stable
 * function for steady identity.
 *
 * Adding a new event:
 *   1. add an entry in specs/contracts/realtime/events.yaml,
 *   2. regenerate via scripts/quality/codegen-frontend.sh,
 *   3. invoke useRealtimeEvent('your.new_event', handler) somewhere in apps/web/src,
 *   4. publish it from a Throne.Application handler via IRealtimeEventPublisher.
 *
 * The realtime quality gate fails until all four are present.
 */
export function useRealtimeEvent<K extends RealtimeEventName>(
  name: K,
  handler: (payload: RealtimeEventMap[K]) => void
): void {
  useEffect(() => {
    const unsubscribe = realtimeClient.subscribe(name, handler);
    return unsubscribe;
  }, [name, handler]);
}
