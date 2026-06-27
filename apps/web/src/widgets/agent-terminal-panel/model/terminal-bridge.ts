import { FitAddon } from "@xterm/addon-fit";
import { Terminal } from "@xterm/xterm";

import type { InputFrame, ResizeFrame } from "./terminal-frames";
import { parseFrame, toWebSocketUrl } from "./terminal-frames";

const RESIZE_DEBOUNCE_MS = 150;
const MAX_PENDING_OUTPUT_CHARS = 1_000_000;

export interface MountTerminalOptions {
  intentId: string;
  onClosed: (code: number) => void;
}

/**
 * xterm парсит цвета темы сам и не понимает `var(--token)`/oklch(), поэтому
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

/**
 * Поднимает xterm + WebSocket-мост к `/api/v1/intents/{id}/terminal/ws`
 * (см. `specs/contracts/realtime/websocket/terminal.yaml`) и возвращает teardown.
 *
 * Вызывать ТОЛЬКО после загрузки шрифта терминала: первый `fit()` должен мерить
 * метрики Monaspace Neon, иначе cols считается по fallback-шрифту (ошибка растёт
 * с шириной) → кривая геометрия в tmux и каша на широких терминалах.
 *
 * Resize'ы шлём через trailing-debounce: на старте контейнер успевает несколько
 * раз сменить размер, а каждый resize-фрейм заставляет TUI-агента перерисоваться
 * и просыпать прошлый кадр в scrollback. Debounce схлопывает эту пачку в один
 * финальный resize, убирая дублирование истории.
 */
export function mountTerminal(
  container: HTMLDivElement,
  { intentId, onClosed }: MountTerminalOptions
): () => void {
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
      cursor: resolveTokenColor("--color-terminal-cursor", "rgb(122, 167, 255)")
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

  const sendResize = () => {
    if (socket.readyState !== WebSocket.OPEN) return;
    if (lastResize.cols === term.cols && lastResize.rows === term.rows) return;
    lastResize = { cols: term.cols, rows: term.rows };
    const frame: ResizeFrame = {
      type: "resize",
      cols: term.cols,
      rows: term.rows
    };
    socket.send(JSON.stringify(frame));
  };

  const scheduleResize = () => {
    if (resizeTimer !== null) window.clearTimeout(resizeTimer);
    resizeTimer = window.setTimeout(() => {
      resizeTimer = null;
      try {
        fitAddon.fit();
        sendResize();
      } catch {
        // ResizeObserver may fire before terminal layout settles.
      }
    }, RESIZE_DEBOUNCE_MS);
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

  const resizeObserver = new ResizeObserver(scheduleResize);
  resizeObserver.observe(container);

  socket.addEventListener("open", () => {
    scheduleResize();
  });

  socket.addEventListener("message", (event) => {
    const frame = parseFrame(event.data);
    if (frame === null) return;
    enqueueOutput(frame.data);
  });

  let closedSignalled = false;
  const signalClosed = (code: number) => {
    // Плановый teardown сам закрывает сокет — его close не означает конец
    // tmux-сессии, поэтому не сигналим наружу.
    if (disposed || closedSignalled) return;
    closedSignalled = true;
    onClosed(code);
  };

  socket.addEventListener("close", (event) => {
    signalClosed(event.code);
  });
  socket.addEventListener("error", () => {
    signalClosed(1006);
  });

  return () => {
    disposed = true;
    if (resizeTimer !== null) window.clearTimeout(resizeTimer);
    if (writeFrame !== null) window.cancelAnimationFrame(writeFrame);
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
}
