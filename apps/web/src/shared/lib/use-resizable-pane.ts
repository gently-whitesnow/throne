import type React from "react";
import { useCallback, useEffect, useRef, useState } from "react";

/** Какой край панели тащит ручка: правый край левой панели или левый край правой. */
export type ResizableEdge = "right" | "left";

export interface ResizablePaneOptions {
  storageKey: string;
  defaultWidth: number;
  min: number;
  max: number;
  /** "right" — ширина растёт по движению вправо; "left" — наоборот (правый рейл). */
  edge: ResizableEdge;
}

export interface ResizablePane {
  width: number;
  onPointerDown: (event: React.PointerEvent<HTMLElement>) => void;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function readWidth(key: string, fallback: number, min: number, max: number) {
  if (typeof window === "undefined") return fallback;
  const raw = window.localStorage.getItem(key);
  if (!raw) return fallback;
  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) ? clamp(parsed, min, max) : fallback;
}

/**
 * Ширина боковой панели с pointer-drag ресайзом и персистом в localStorage.
 * Ключ задаётся вызывающим с учётом роли рейла, чтобы разные панели не делили
 * одно значение.
 */
export function useResizablePane({
  storageKey,
  defaultWidth,
  min,
  max,
  edge
}: ResizablePaneOptions): ResizablePane {
  const [width, setWidth] = useState(() =>
    readWidth(storageKey, defaultWidth, min, max)
  );

  useEffect(() => {
    window.localStorage.setItem(storageKey, String(width));
  }, [storageKey, width]);

  const widthRef = useRef(width);
  widthRef.current = width;

  const onPointerDown = useCallback(
    (event: React.PointerEvent<HTMLElement>) => {
      event.preventDefault();
      const startX = event.clientX;
      const startWidth = widthRef.current;
      const sign = edge === "left" ? -1 : 1;
      const previousCursor = document.body.style.cursor;
      const previousSelect = document.body.style.userSelect;
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";

      const handleMove = (moveEvent: PointerEvent) => {
        const delta = (moveEvent.clientX - startX) * sign;
        setWidth(clamp(startWidth + delta, min, max));
      };
      const handleUp = () => {
        document.body.style.cursor = previousCursor;
        document.body.style.userSelect = previousSelect;
        window.removeEventListener("pointermove", handleMove);
        window.removeEventListener("pointerup", handleUp);
      };
      window.addEventListener("pointermove", handleMove);
      window.addEventListener("pointerup", handleUp);
    },
    [edge, min, max]
  );

  return { width, onPointerDown };
}
