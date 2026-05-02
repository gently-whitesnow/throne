import { apiUrl } from "@/shared/api";

import type { RealtimeEventMap, RealtimeEventName } from "./generated/events";
import { realtimeEventNames } from "./generated/events";

type Handler<K extends RealtimeEventName> = (
  payload: RealtimeEventMap[K]
) => void;

interface InternalHandler {
  readonly name: RealtimeEventName;
  readonly fn: (payload: unknown) => void;
}

/**
 * Singleton EventSource wrapper. Lazy-connects on the first subscriber, reconnects
 * automatically (browser EventSource default), closes when the last subscriber unsubscribes.
 *
 * Adding a new event requires extending specs/contracts/realtime/events.yaml — the
 * generated `realtimeEventNames` drives both the SSE listener registration and the
 * realtime quality gate.
 */
class RealtimeClient {
  private source: EventSource | null = null;
  private readonly handlers = new Set<InternalHandler>();
  private readonly listeners = new Map<RealtimeEventName, EventListener>();

  subscribe<K extends RealtimeEventName>(
    name: K,
    handler: Handler<K>
  ): () => void {
    const entry: InternalHandler = {
      name,
      fn: (payload) => {
        handler(payload as RealtimeEventMap[K]);
      }
    };
    this.handlers.add(entry);
    this.ensureConnected();

    return () => {
      this.handlers.delete(entry);
      if (this.handlers.size === 0) {
        this.disconnect();
      }
    };
  }

  private ensureConnected(): void {
    if (this.source) return;

    const source = new EventSource(apiUrl("/realtime/stream"));
    this.source = source;

    for (const name of realtimeEventNames) {
      const listener: EventListener = (event) => {
        const messageEvent = event as MessageEvent<string>;
        let payload: unknown;
        try {
          payload = JSON.parse(messageEvent.data) as unknown;
        } catch {
          return;
        }
        for (const h of this.handlers) {
          if (h.name === name) {
            h.fn(payload);
          }
        }
      };
      this.listeners.set(name, listener);
      source.addEventListener(name, listener);
    }
  }

  private disconnect(): void {
    if (!this.source) return;
    for (const [name, listener] of this.listeners) {
      this.source.removeEventListener(name, listener);
    }
    this.listeners.clear();
    this.source.close();
    this.source = null;
  }
}

export const realtimeClient = new RealtimeClient();
