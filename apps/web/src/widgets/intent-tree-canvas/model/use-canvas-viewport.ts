import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type RefObject,
  type WheelEvent as ReactWheelEvent
} from "react";

export interface Viewport {
  x: number;
  y: number;
  scale: number;
}

export interface WorldBounds {
  w: number;
  h: number;
}

const MIN_SCALE = 0.35;
const MAX_SCALE = 1.6;
const FIT_PADDING = 64;
// Pan can park the world so its near edge sits at most this far past the stage
// edge — keeps a strip of content visible no matter how hard the user flings.
const PAN_MARGIN = 160;

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function clampPan(
  x: number,
  y: number,
  scale: number,
  stageW: number,
  stageH: number,
  worldW: number,
  worldH: number
): { x: number; y: number } {
  const contentW = worldW * scale;
  const contentH = worldH * scale;
  const xMin = Math.min(PAN_MARGIN - contentW, stageW - contentW - PAN_MARGIN);
  const xMax = Math.max(stageW - PAN_MARGIN, PAN_MARGIN);
  const yMin = Math.min(PAN_MARGIN - contentH, stageH - contentH - PAN_MARGIN);
  const yMax = Math.max(stageH - PAN_MARGIN, PAN_MARGIN);
  return {
    x: clamp(x, xMin, xMax),
    y: clamp(y, yMin, yMax)
  };
}

interface UseCanvasViewportArgs {
  stageRef: RefObject<HTMLDivElement | null>;
  worldBounds: WorldBounds | null;
}

interface UseCanvasViewportResult {
  viewport: Viewport;
  isPanning: boolean;
  fitToView: () => void;
  zoomIn: () => void;
  zoomOut: () => void;
  handlers: {
    onPointerDown: (e: ReactPointerEvent<HTMLDivElement>) => void;
    onPointerMove: (e: ReactPointerEvent<HTMLDivElement>) => void;
    onPointerUp: (e: ReactPointerEvent<HTMLDivElement>) => void;
    onPointerCancel: (e: ReactPointerEvent<HTMLDivElement>) => void;
    onWheel: (e: ReactWheelEvent<HTMLDivElement>) => void;
  };
}

export function useCanvasViewport({
  stageRef,
  worldBounds
}: UseCanvasViewportArgs): UseCanvasViewportResult {
  const [viewport, setViewport] = useState<Viewport>({
    x: FIT_PADDING,
    y: FIT_PADDING,
    scale: 1
  });
  const [isPanning, setIsPanning] = useState(false);
  const panStateRef = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    startVx: number;
    startVy: number;
  } | null>(null);

  const fitToView = useCallback(() => {
    const stage = stageRef.current;
    if (!stage || !worldBounds) return;
    const { w, h } = worldBounds;
    if (w === 0 || h === 0) return;
    const W = stage.clientWidth;
    const H = stage.clientHeight;
    if (W === 0 || H === 0) return;
    const sx = (W - FIT_PADDING * 2) / w;
    const sy = (H - FIT_PADDING * 2) / h;
    const scale = clamp(Math.min(sx, sy, 1), MIN_SCALE, MAX_SCALE);
    const offX = (W - w * scale) / 2;
    const offY = (H - h * scale) / 2;
    setViewport({ x: offX, y: offY, scale });
  }, [stageRef, worldBounds]);

  // Re-fit when bounds reshape — tracked via stable string key on dims so we
  // don't re-fit on every viewport tick.
  const boundsKey = worldBounds
    ? `${String(worldBounds.w)}x${String(worldBounds.h)}`
    : null;
  useLayoutEffect(() => {
    if (boundsKey === null) return;
    fitToView();
  }, [boundsKey, fitToView]);

  const onPointerDown = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (e.button !== 0 && e.button !== 1) return;
    panStateRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      startVx: viewport.x,
      startVy: viewport.y
    };
    e.currentTarget.setPointerCapture(e.pointerId);
    setIsPanning(true);
  };

  const onPointerMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    const pan = panStateRef.current;
    if (pan?.pointerId !== e.pointerId) return;
    const stage = stageRef.current;
    const nextX = pan.startVx + (e.clientX - pan.startX);
    const nextY = pan.startVy + (e.clientY - pan.startY);
    if (!stage || !worldBounds || worldBounds.w === 0 || worldBounds.h === 0) {
      setViewport({ x: nextX, y: nextY, scale: viewport.scale });
      return;
    }
    const clamped = clampPan(
      nextX,
      nextY,
      viewport.scale,
      stage.clientWidth,
      stage.clientHeight,
      worldBounds.w,
      worldBounds.h
    );
    setViewport({ ...clamped, scale: viewport.scale });
  };

  const endPan = (e: ReactPointerEvent<HTMLDivElement>) => {
    const pan = panStateRef.current;
    if (pan?.pointerId !== e.pointerId) return;
    e.currentTarget.releasePointerCapture(e.pointerId);
    panStateRef.current = null;
    setIsPanning(false);
  };

  const onWheel = (e: ReactWheelEvent<HTMLDivElement>) => {
    const stage = stageRef.current;
    if (!stage) return;
    const W = stage.clientWidth;
    const H = stage.clientHeight;
    const hasBounds =
      worldBounds !== null && worldBounds.w > 0 && worldBounds.h > 0;

    if (e.ctrlKey || e.metaKey) {
      e.preventDefault();
      const rect = stage.getBoundingClientRect();
      const sx = e.clientX - rect.left;
      const sy = e.clientY - rect.top;
      const factor = Math.exp(-e.deltaY * 0.002);
      const ns = clamp(viewport.scale * factor, MIN_SCALE, MAX_SCALE);
      const wx = (sx - viewport.x) / viewport.scale;
      const wy = (sy - viewport.y) / viewport.scale;
      const nx = sx - wx * ns;
      const ny = sy - wy * ns;
      if (!hasBounds) {
        setViewport({ scale: ns, x: nx, y: ny });
        return;
      }
      const clamped = clampPan(nx, ny, ns, W, H, worldBounds.w, worldBounds.h);
      setViewport({ scale: ns, ...clamped });
      return;
    }

    setViewport((p) => {
      const nx = p.x - e.deltaX;
      const ny = p.y - e.deltaY;
      if (!hasBounds) return { ...p, x: nx, y: ny };
      const clamped = clampPan(
        nx,
        ny,
        p.scale,
        W,
        H,
        worldBounds.w,
        worldBounds.h
      );
      return { ...p, ...clamped };
    });
  };

  const zoomIn = useCallback(() => {
    setViewport((p) => ({
      ...p,
      scale: clamp(p.scale * 1.15, MIN_SCALE, MAX_SCALE)
    }));
  }, []);
  const zoomOut = useCallback(() => {
    setViewport((p) => ({
      ...p,
      scale: clamp(p.scale * 0.87, MIN_SCALE, MAX_SCALE)
    }));
  }, []);

  // Suppress native page zoom when the user is wheel-zooming the stage.
  useEffect(() => {
    const stage = stageRef.current;
    if (!stage) return;
    const preventCtrlWheel = (e: WheelEvent) => {
      if (e.ctrlKey || e.metaKey) e.preventDefault();
    };
    stage.addEventListener("wheel", preventCtrlWheel, { passive: false });
    return () => {
      stage.removeEventListener("wheel", preventCtrlWheel);
    };
  }, [stageRef]);

  return {
    viewport,
    isPanning,
    fitToView,
    zoomIn,
    zoomOut,
    handlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp: endPan,
      onPointerCancel: endPan,
      onWheel
    }
  };
}
