import type React from "react";
import { useCallback, useEffect, useRef, useState } from "react";
import { Outlet, useParams } from "react-router-dom";

import { IntentBoard } from "@/widgets/intent-board";
import { IntentContextRail } from "@/widgets/intent-context-rail";

const RAIL_KEY = "throne.layout.intents.rail";
const BOARD_KEY = "throne.layout.intents.board";
const RAIL_DEFAULT = 200;
const BOARD_DEFAULT = 320;
const RAIL_MIN = 140;
const RAIL_MAX = 480;
const BOARD_MIN = 220;
const BOARD_MAX = 640;

function readWidth(key: string, fallback: number): number {
  if (typeof window === "undefined") return fallback;
  const raw = window.localStorage.getItem(key);
  if (!raw) return fallback;
  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function IntentsSectionPage() {
  const { id } = useParams<{ id?: string }>();

  const [railWidth, setRailWidth] = useState(() =>
    clamp(readWidth(RAIL_KEY, RAIL_DEFAULT), RAIL_MIN, RAIL_MAX)
  );
  const [boardWidth, setBoardWidth] = useState(() =>
    clamp(readWidth(BOARD_KEY, BOARD_DEFAULT), BOARD_MIN, BOARD_MAX)
  );

  useEffect(() => {
    window.localStorage.setItem(RAIL_KEY, String(railWidth));
  }, [railWidth]);
  useEffect(() => {
    window.localStorage.setItem(BOARD_KEY, String(boardWidth));
  }, [boardWidth]);

  const startRailDrag = useDividerDrag((deltaX, startWidth) => {
    setRailWidth(clamp(startWidth + deltaX, RAIL_MIN, RAIL_MAX));
  }, railWidth);
  const startBoardDrag = useDividerDrag((deltaX, startWidth) => {
    setBoardWidth(clamp(startWidth + deltaX, BOARD_MIN, BOARD_MAX));
  }, boardWidth);

  return (
    <div className="flex h-screen overflow-hidden max-md:grid max-md:grid-cols-1 max-md:grid-rows-[minmax(120px,26vh)_minmax(160px,32vh)_1fr]">
      <div
        className="grid min-h-0 min-w-0 max-md:!w-auto"
        style={{ width: railWidth, flexShrink: 0 }}
      >
        <IntentContextRail />
      </div>
      <ResizeHandle
        ariaLabel="Изменить ширину панели контекстов"
        onPointerDown={startRailDrag}
      />
      <div
        className="grid min-h-0 min-w-0 max-md:!w-auto"
        style={{ width: boardWidth, flexShrink: 0 }}
      >
        <IntentBoard />
      </div>
      <ResizeHandle
        ariaLabel="Изменить ширину списка intents"
        onPointerDown={startBoardDrag}
      />
      <section
        className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden"
        aria-label="Детали Intent"
      >
        {id ? (
          <Outlet />
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-sm text-base-content/60">
            <p className="m-0">Выберите Intent в списке</p>
            <p className="m-0 text-xs text-base-content/60">
              Слева — контексты, рядом — intents выбранного контекста.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}

function ResizeHandle({
  ariaLabel,
  onPointerDown
}: {
  ariaLabel: string;
  onPointerDown: (event: React.PointerEvent<HTMLDivElement>) => void;
}) {
  return (
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label={ariaLabel}
      onPointerDown={onPointerDown}
      className="group relative hidden w-px flex-shrink-0 cursor-col-resize bg-base-300 transition-colors hover:bg-primary/40 md:block"
    >
      <span
        aria-hidden
        className="absolute inset-y-0 -left-1 -right-1 block group-hover:bg-primary/10"
      />
    </div>
  );
}

function useDividerDrag(
  apply: (deltaX: number, startWidth: number) => void,
  currentWidth: number
) {
  const widthRef = useRef(currentWidth);
  widthRef.current = currentWidth;

  return useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      event.preventDefault();
      const startX = event.clientX;
      const startWidth = widthRef.current;
      const previousCursor = document.body.style.cursor;
      const previousSelect = document.body.style.userSelect;
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";

      const handleMove = (moveEvent: PointerEvent) => {
        apply(moveEvent.clientX - startX, startWidth);
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
    [apply]
  );
}
