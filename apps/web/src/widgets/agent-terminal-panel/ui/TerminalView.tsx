import "@xterm/xterm/css/xterm.css";

import { FitAddon } from "@xterm/addon-fit";
import { Terminal } from "@xterm/xterm";
import { GripHorizontal } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

import type { InputFrame, ResizeFrame } from "../model/terminal-frames";
import { parseFrame, toWebSocketUrl } from "../model/terminal-frames";

interface TerminalViewProps {
  intentId: string;
  /** Per-attempt nonce — bump to force a fresh xterm + WebSocket. */
  attempt: number;
  onClosed: (code: number) => void;
}

/**
 * xterm parses theme colours itself и не понимает `var(--token)`/oklch(), поэтому
 * резолвим дизайн-токен в конкретный rgb руками браузера (probe-элемент). Fallback
 * (rgb, не hex) держит детерминизм в jsdom, где computed-цвет пустой.
 */
function resolveTokenColor(token: string, fallback: string): string {
  if (typeof document === "undefined") return fallback;
  const probe = document.createElement("span");
  probe.style.color = `var(${token})`;
  probe.style.position = "absolute";
  probe.style.visibility = "hidden";
  document.body.appendChild(probe);
  const resolved = getComputedStyle(probe).color;
  probe.remove();
  return resolved || fallback;
}

const MIN_TERMINAL_HEIGHT = 200;
const MAX_TERMINAL_HEIGHT = 1000;
const DEFAULT_TERMINAL_HEIGHT = 576;
const RESIZE_THROTTLE_MS = 100;
const MAX_PENDING_OUTPUT_CHARS = 1_000_000;

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
  const [height, setHeight] = useState(DEFAULT_TERMINAL_HEIGHT);
  const dragRef = useRef<{ startY: number; startHeight: number } | null>(null);

  const handlePointerDown = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      event.preventDefault();
      dragRef.current = { startY: event.clientY, startHeight: height };
      event.currentTarget.setPointerCapture(event.pointerId);
    },
    [height]
  );

  const handlePointerMove = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      const drag = dragRef.current;
      if (drag === null) return;
      // Тянем ручку вверх (clientY уменьшается) → окно растёт вверх.
      const next = drag.startHeight + (drag.startY - event.clientY);
      setHeight(
        Math.min(Math.max(next, MIN_TERMINAL_HEIGHT), MAX_TERMINAL_HEIGHT)
      );
    },
    []
  );

  const handlePointerUp = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      dragRef.current = null;
      event.currentTarget.releasePointerCapture(event.pointerId);
    },
    []
  );

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
        background: resolveTokenColor("--color-terminal-bg", "rgb(15, 18, 23)"),
        foreground: resolveTokenColor(
          "--color-terminal-fg",
          "rgb(230, 232, 238)"
        ),
        cursor: resolveTokenColor(
          "--color-terminal-cursor",
          "rgb(122, 167, 255)"
        )
      }
    });
    const fitAddon = new FitAddon();
    term.loadAddon(fitAddon);
    term.open(container);
    fitAddon.fit();

    const socket = new WebSocket(toWebSocketUrl(intentId));
    socket.binaryType = "arraybuffer";

    let disposed = false;
    let lastResize = { cols: 0, rows: 0 };
    let resizeTimer: number | null = null;
    let pendingOutput = "";
    let writeFrame: number | null = null;

    const sendResize = (force = false) => {
      if (socket.readyState !== WebSocket.OPEN) return;
      if (
        !force &&
        lastResize.cols === term.cols &&
        lastResize.rows === term.rows
      ) {
        return;
      }
      lastResize = { cols: term.cols, rows: term.rows };
      const frame: ResizeFrame = {
        type: "resize",
        cols: term.cols,
        rows: term.rows
      };
      socket.send(JSON.stringify(frame));
    };

    let fontsLoaded = false;
    let socketOpened = false;
    let initialGeometrySent = false;

    // Первую геометрию tmux'у задаёт первый resize-фрейм, поэтому он обязан
    // считаться на метриках уже загруженного Monaspace Neon. Измерение на
    // fallback-шрифте даёт неверную ширину клетки → неверный cols, и абсолютная
    // ошибка в колонках растёт с шириной (≈4 при 80, ≈12 при 240) — отсюда каша
    // только на широких терминалах. Ждём И шрифт, И открытый сокет, после чего
    // шлём единственный корректный resize и форсим redraw приложения (C-l),
    // вычищая остаточный мусор без ручного ресайза панели.
    const sendInitialGeometry = () => {
      if (disposed || initialGeometrySent) return;
      if (!fontsLoaded || !socketOpened) return;
      initialGeometrySent = true;
      try {
        fitAddon.fit();
      } catch {
        // layout may not be settled yet
      }
      sendResize(true);
      const redraw: InputFrame = { type: "input", data: "\f" };
      socket.send(JSON.stringify(redraw));
    };

    const fontSet = document.fonts as FontFaceSet | undefined;
    void Promise.resolve(fontSet?.load('13px "Monaspace Neon"'))
      .catch(() => undefined)
      .then(() => fontSet?.ready)
      .then(() => {
        fontsLoaded = true;
        sendInitialGeometry();
      });

    const scheduleResize = () => {
      if (resizeTimer !== null) return;
      resizeTimer = window.setTimeout(() => {
        resizeTimer = null;
        try {
          fitAddon.fit();
          sendResize();
        } catch {
          // ResizeObserver may fire before terminal layout settles.
        }
      }, RESIZE_THROTTLE_MS);
    };

    const flushOutput = () => {
      writeFrame = null;
      if (disposed || pendingOutput.length === 0) return;
      const data = pendingOutput;
      pendingOutput = "";
      term.write(data);
    };

    const scheduleOutputWrite = () => {
      if (writeFrame !== null) return;
      writeFrame = window.requestAnimationFrame(flushOutput);
    };

    const enqueueOutput = (data: string) => {
      if (pendingOutput.length + data.length > MAX_PENDING_OUTPUT_CHARS) {
        pendingOutput = pendingOutput.slice(-MAX_PENDING_OUTPUT_CHARS / 2);
      }
      pendingOutput += data;
      scheduleOutputWrite();
    };

    const inputDisposable = term.onData((data) => {
      if (socket.readyState !== WebSocket.OPEN) return;
      const frame: InputFrame = { type: "input", data };
      socket.send(JSON.stringify(frame));
    });

    const resizeObserver = new ResizeObserver(() => {
      scheduleResize();
    });
    resizeObserver.observe(container);

    socket.addEventListener("open", () => {
      socketOpened = true;
      sendInitialGeometry();
    });

    socket.addEventListener("message", (event) => {
      const frame = parseFrame(event.data);
      if (frame === null) return;
      enqueueOutput(frame.data);
    });

    let closedSignalled = false;
    const signalClosed = (code: number) => {
      // Плановый teardown (cleanup эффекта) сам закрывает сокет — его close
      // не означает конец tmux-сессии, поэтому не сигналим наружу.
      if (disposed || closedSignalled) return;
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
      disposed = true;
      if (resizeTimer !== null) {
        window.clearTimeout(resizeTimer);
      }
      if (writeFrame !== null) {
        window.cancelAnimationFrame(writeFrame);
      }
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
  }, [intentId, attempt, onClosed]);

  return (
    <div className="flex flex-col">
      <div
        role="separator"
        aria-orientation="horizontal"
        aria-label="Изменить высоту терминала"
        data-testid="agent-terminal-resize-handle"
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        className="flex cursor-ns-resize items-center justify-center rounded-t-md border border-b-0 border-base-300 bg-base-200 py-1 text-base-content/40 hover:text-base-content/70 touch-none select-none"
      >
        <GripHorizontal aria-hidden size={16} strokeWidth={2} />
      </div>
      <div
        ref={containerRef}
        data-testid="agent-terminal-xterm"
        style={{ height }}
        className="w-full overflow-hidden rounded-b-md border border-base-300 bg-[var(--color-terminal-bg)] p-2"
      />
    </div>
  );
}
