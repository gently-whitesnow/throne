import { FitAddon } from "@xterm/addon-fit";
import { Terminal } from "@xterm/xterm";

import type { InputFrame, ResizeFrame } from "./terminal-frames";
import { parseFrame, toWebSocketUrl } from "./terminal-frames";

const RESIZE_DEBOUNCE_MS = 150;
const MAX_PENDING_OUTPUT_CHARS = 1_000_000;

// Cell-size-determining options shared by the live terminal and the pre-spawn measurement,
// so the geometry measured for the spawn matches what the live xterm fits to (no reflow).
const TERMINAL_METRICS = {
  fontFamily: "Monaspace Neon, ui-monospace, Menlo, Consolas, monospace",
  fontSize: 13,
  lineHeight: 1.3
} as const;

// Layout classes of the live xterm host (TerminalView's container) that affect the fit:
// width box + padding + border. Replicated on the off-screen measuring node so cols/rows match.
const HOST_LAYOUT_CLASS =
  "w-full overflow-hidden rounded-b-md border border-base-300 p-2";

export interface MountTerminalOptions {
  intentId: string;
  onClosed: (code: number) => void;
}

/**
 * Меряет геометрию терминала ДО спавна, повторяя бокс живого контейнера: создаёт скрытый
 * узел той же ширины/паддинга/высоты внутри `parent`, монтирует временный xterm с теми же
 * метриками шрифта и возвращает cols/rows из `proposeDimensions`. Сервер спавнит сессию в
 * этой геометрии, поэтому первый resize клиента — no-op (без reflow и дублей в scrollback).
 *
 * Вызывать после загрузки шрифта: на fallback-метриках замер разойдётся с живым fit.
 * Возвращает null, если замер не удался — вызывающий просто спавнит без геометрии (старое
 * поведение: один reflow при первом attach).
 */
export function measureViewport(
  parent: HTMLElement,
  heightPx: number
): { cols: number; rows: number } | null {
  const root = document.createElement("div");
  root.className = "flex flex-col";
  root.style.visibility = "hidden";
  const host = document.createElement("div");
  host.className = HOST_LAYOUT_CLASS;
  host.style.height = `${String(heightPx)}px`;
  root.appendChild(host);
  parent.appendChild(root);

  let term: Terminal | null = null;
  try {
    term = new Terminal(TERMINAL_METRICS);
    const fit = new FitAddon();
    term.loadAddon(fit);
    term.open(host);
    const dims = fit.proposeDimensions();
    if (!dims) return null;
    const cols = Math.floor(dims.cols);
    const rows = Math.floor(dims.rows);
    return cols > 0 && rows > 0 ? { cols, rows } : null;
  } catch {
    return null;
  } finally {
    term?.dispose();
    parent.removeChild(root);
  }
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
    ...TERMINAL_METRICS,
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
