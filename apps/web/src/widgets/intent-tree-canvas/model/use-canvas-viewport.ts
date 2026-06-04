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
  // Идентичность графа (контекст канваса). Авто-fit срабатывает один раз на
  // каждое новое значение; смена статуса/прилёт SSE меняют bounds, но не
  // fitKey — значит viewport пользователя не сбрасывается.
  fitKey: string;
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
  worldBounds,
  fitKey
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

  // Зависим от примитивов w/h, а не от объекта worldBounds: иначе любой
  // ререндер с новой ref (например, точечный setItems в useTreeData) менял бы
  // identity fitToView и useLayoutEffect ниже сбрасывал бы viewport.
  const worldW = worldBounds?.w ?? 0;
  const worldH = worldBounds?.h ?? 0;

  const fitToView = useCallback(() => {
    const stage = stageRef.current;
    if (!stage || worldW === 0 || worldH === 0) return;
    const W = stage.clientWidth;
    const H = stage.clientHeight;
    if (W === 0 || H === 0) return;
    const sx = (W - FIT_PADDING * 2) / worldW;
    const sy = (H - FIT_PADDING * 2) / worldH;
    const scale = clamp(Math.min(sx, sy, 1), MIN_SCALE, MAX_SCALE);
    const offX = (W - worldW * scale) / 2;
    const offY = (H - worldH * scale) / 2;
    setViewport({ x: offX, y: offY, scale });
  }, [stageRef, worldW, worldH]);

  // Авто-fit один раз на каждый граф (fitKey = контекст): первая загрузка и
  // переключение контекста. Последующие изменения bounds от realtime-апдейтов
  // (intent → done, прилёт SSE) НЕ должны дёргать viewport — иначе pan/zoom
  // пользователя сбрасывается на каждую смену статуса.
  const fittedKeyRef = useRef<string | null>(null);
  useLayoutEffect(() => {
    if (worldW === 0 || worldH === 0) return;
    if (fittedKeyRef.current === fitKey) return;
    fittedKeyRef.current = fitKey;
    fitToView();
  }, [worldW, worldH, fitToView, fitKey]);

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
      const factor = Math.exp(-e.deltaY * 0.005);
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
