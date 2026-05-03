import { useRealtimeEvent } from "@/shared/realtime";

/**
 * Hook seam for the dream loop. Intent 5 wires real UI on top of these subscribers
 * (fuel meter + pending dream-runs widget); for now the hook just keeps the realtime
 * contract gate satisfied and lets callers force a refresh on any dream.* event.
 */
export function useDreamRealtime(onChange: () => void): void {
  useRealtimeEvent("dream.run_created", onChange);
  useRealtimeEvent("dream.proposal_created", onChange);
  useRealtimeEvent("dream.proposal_applied", onChange);
  useRealtimeEvent("dream.proposal_skipped", onChange);
  useRealtimeEvent("dream.run_closed", onChange);
  useRealtimeEvent("dream.fuel_changed", onChange);
}
