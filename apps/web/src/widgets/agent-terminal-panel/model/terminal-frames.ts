import { apiUrl } from "@/shared/api";
import { terminalWebSocketEndpoints } from "@/shared/realtime";

/**
 * Кадры WebSocket-моста к `/api/v1/intents/{id}/terminal/ws`
 * (см. `specs/contracts/realtime/websocket/terminal.yaml`).
 */
export interface InputFrame {
  type: "input";
  data: string;
}

export interface ResizeFrame {
  type: "resize";
  cols: number;
  rows: number;
}

export interface OutputFrame {
  type: "output";
  data: string;
}

export type IncomingFrame = OutputFrame;

export function parseFrame(raw: unknown): IncomingFrame | null {
  if (typeof raw !== "string") return null;
  try {
    const parsed = JSON.parse(raw) as Partial<IncomingFrame>;
    if (parsed.type === "output" && typeof parsed.data === "string") {
      return { type: "output", data: parsed.data };
    }
  } catch {
    return null;
  }
  return null;
}

export function toWebSocketUrl(intentId: string): string {
  const httpUrl = apiUrl(
    terminalWebSocketEndpoints.intentsTerminalWs(intentId)
  );
  if (httpUrl.startsWith("http://") || httpUrl.startsWith("https://")) {
    return httpUrl.replace(/^http/, "ws");
  }
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  const path = httpUrl.startsWith("/") ? httpUrl : `/${httpUrl}`;
  return `${protocol}//${window.location.host}${path}`;
}
