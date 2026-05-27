import "@xterm/xterm/css/xterm.css";

import { FitAddon } from "@xterm/addon-fit";
import { Terminal } from "@xterm/xterm";
import { useEffect, useRef } from "react";

import { apiUrl } from "@/shared/api";

interface TerminalViewProps {
  intentId: string;
  /** Per-attempt nonce — bump to force a fresh xterm + WebSocket. */
  attempt: number;
  onClosed: (code: number) => void;
}

interface InputFrame {
  type: "input";
  data: string;
}

interface ResizeFrame {
  type: "resize";
  cols: number;
  rows: number;
}

interface OutputFrame {
  type: "output";
  data: string;
}

type IncomingFrame = OutputFrame;

/**
 * Тонкая обёртка над xterm.js + WebSocket-bridge'ом к
 * `/api/v1/intents/{id}/terminal/ws` (см. `specs/contracts/realtime/websocket/terminal.yaml`).
 * Все session-lifecycle решения принимаются снаружи — компонент только
 * монтирует терминал и пробрасывает кадры input/resize/output.
 */
export function TerminalView({
  intentId,
  attempt,
  onClosed
}: TerminalViewProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const term = new Terminal({
      fontFamily: "Monaspace Neon, ui-monospace, Menlo, Consolas, monospace",
      fontSize: 13,
      lineHeight: 1.3,
      cursorBlink: true,
      convertEol: true,
      scrollback: 5000,
      theme: {
        background: "#0F1217",
        foreground: "#E6E8EE",
        cursor: "#7AA7FF"
      }
    });
    const fitAddon = new FitAddon();
    term.loadAddon(fitAddon);
    term.open(container);
    fitAddon.fit();

    const socket = new WebSocket(toWebSocketUrl(intentId));
    socket.binaryType = "arraybuffer";

    const sendResize = () => {
      if (socket.readyState !== WebSocket.OPEN) return;
      const frame: ResizeFrame = {
        type: "resize",
        cols: term.cols,
        rows: term.rows
      };
      socket.send(JSON.stringify(frame));
    };

    const inputDisposable = term.onData((data) => {
      if (socket.readyState !== WebSocket.OPEN) return;
      const frame: InputFrame = { type: "input", data };
      socket.send(JSON.stringify(frame));
    });

    const resizeObserver = new ResizeObserver(() => {
      try {
        fitAddon.fit();
        sendResize();
      } catch {
        // ResizeObserver may fire before terminal layout settles.
      }
    });
    resizeObserver.observe(container);

    socket.addEventListener("open", () => {
      fitAddon.fit();
      sendResize();
    });

    socket.addEventListener("message", (event) => {
      const frame = parseFrame(event.data);
      if (frame === null) return;
      term.write(frame.data);
    });

    let closedSignalled = false;
    const signalClosed = (code: number) => {
      if (closedSignalled) return;
      closedSignalled = true;
      onClosed(code);
    };

    socket.addEventListener("close", (event) => {
      signalClosed(event.code);
    });
    socket.addEventListener("error", () => {
      // Treat connection errors as a synthetic close so the UI can recover.
      signalClosed(1006);
    });

    return () => {
      inputDisposable.dispose();
      resizeObserver.disconnect();
      if (
        socket.readyState === WebSocket.OPEN ||
        socket.readyState === WebSocket.CONNECTING
      ) {
        socket.close();
      }
      term.dispose();
    };
    // attempt участвует как nonce — при инкременте монтируется свежий
    // терминал и сокет, чтобы reattach был чистый.
  }, [intentId, attempt, onClosed]);

  return (
    <div
      ref={containerRef}
      data-testid="agent-terminal-xterm"
      className="h-72 w-full overflow-hidden rounded-md border border-base-300 bg-[#0F1217] p-2"
    />
  );
}

function parseFrame(raw: unknown): IncomingFrame | null {
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

function toWebSocketUrl(intentId: string): string {
  const httpUrl = apiUrl(`/intents/${intentId}/terminal/ws`);
  if (httpUrl.startsWith("http://") || httpUrl.startsWith("https://")) {
    return httpUrl.replace(/^http/, "ws");
  }
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  const path = httpUrl.startsWith("/") ? httpUrl : `/${httpUrl}`;
  return `${protocol}//${window.location.host}${path}`;
}
