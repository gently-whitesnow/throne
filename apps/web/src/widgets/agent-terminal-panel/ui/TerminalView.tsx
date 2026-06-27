import "@xterm/xterm/css/xterm.css";

import { GripHorizontal } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

import { mountTerminal } from "../model/terminal-bridge";

interface TerminalViewProps {
  intentId: string;
  /** Per-attempt nonce — bump to force a fresh xterm + WebSocket. */
  attempt: number;
  onClosed: (code: number) => void;
}

const MIN_TERMINAL_HEIGHT = 200;
const MAX_TERMINAL_HEIGHT = 1000;
/** Высота терминала при свежем монтировании — её же берёт pre-spawn замер геометрии. */
export const DEFAULT_TERMINAL_HEIGHT = 576;
const TERMINAL_FONT = '13px "Monaspace Neon"';

/**
 * Тонкая обёртка над xterm.js + WebSocket-bridge'ом (см. `mountTerminal`).
 * Все session-lifecycle решения принимаются снаружи — компонент только
 * монтирует терминал и тянет ручку высоты.
 *
 * Терминал поднимаем ТОЛЬКО после загрузки веб-шрифта: иначе первый `fit()`
 * меряет метрики fallback-шрифта → неверный cols, и в tmux уходит кривая
 * геометрия (каша на широких терминалах + перерисовки, дублирующие scrollback).
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

    let disposed = false;
    let teardown: (() => void) | null = null;

    const fontSet = document.fonts as FontFaceSet | undefined;
    void Promise.resolve(fontSet?.load(TERMINAL_FONT))
      .catch(() => undefined)
      .then(() => fontSet?.ready)
      .then(() => {
        if (disposed) return;
        teardown = mountTerminal(container, { intentId, onClosed });
      });

    return () => {
      disposed = true;
      teardown?.();
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
