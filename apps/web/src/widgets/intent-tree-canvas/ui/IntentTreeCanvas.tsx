import { Move } from "lucide-react";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type WheelEvent as ReactWheelEvent
} from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";

import { contextTitle } from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";

import { useTreeData } from "../model/use-tree-data";
import { EdgeLayer, type Edge } from "./EdgeLayer";
import { TreeCard } from "./TreeCard";
import { ZoomToolbar } from "./ZoomToolbar";

interface Viewport {
  x: number;
  y: number;
  scale: number;
}

const MIN_SCALE = 0.35;
const MAX_SCALE = 1.6;
const FIT_PADDING = 64;

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function IntentTreeCanvas() {
  const navigate = useNavigate();
  const { id: activeId } = useParams<{ id?: string }>();
  const [params] = useSearchParams();
  const context = params.get("context");
  const search = params.toString();

  const state = useTreeData(context);
  const stageRef = useRef<HTMLDivElement | null>(null);
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

  // Edges derived from current nodes — child.id ← parent.id pairs.
  const edges = useMemo<Edge[]>(() => {
    if (state.kind !== "ready") return [];
    const out: Edge[] = [];
    for (const node of state.model.nodes) {
      for (const p of node.parents) out.push({ from: p, to: node.id });
    }
    return out;
  }, [state]);

  // Highlight set: ancestors + descendants of the active card.
  const relatedIds = useMemo<ReadonlySet<string> | null>(() => {
    if (state.kind !== "ready") return null;
    if (!activeId) return null;
    if (!state.model.byId.has(activeId)) return null;

    const childrenOf = new Map<string, string[]>();
    for (const n of state.model.nodes) childrenOf.set(n.id, []);
    for (const n of state.model.nodes) {
      for (const p of n.parents) {
        const bucket = childrenOf.get(p);
        if (bucket) bucket.push(n.id);
      }
    }

    const seen = new Set<string>([activeId]);
    const goUp = (id: string): void => {
      const node = state.model.byId.get(id);
      if (!node) return;
      for (const p of node.parents) {
        if (!seen.has(p)) {
          seen.add(p);
          goUp(p);
        }
      }
    };
    const goDown = (id: string): void => {
      const kids = childrenOf.get(id) ?? [];
      for (const k of kids) {
        if (!seen.has(k)) {
          seen.add(k);
          goDown(k);
        }
      }
    };
    goUp(activeId);
    goDown(activeId);
    return seen;
  }, [activeId, state]);

  const fitToView = useCallback(() => {
    const stage = stageRef.current;
    if (!stage) return;
    if (state.kind !== "ready") return;
    const { w, h } = state.model.bounds;
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
  }, [state]);

  // Fit when the layout becomes available or its bounds change. The dependency
  // tracks the bounds object identity so we re-fit when the graph reshapes.
  const boundsKey =
    state.kind === "ready"
      ? `${String(state.model.bounds.w)}x${String(state.model.bounds.h)}`
      : null;
  useLayoutEffect(() => {
    if (boundsKey === null) return;
    fitToView();
    // We intentionally re-fit only on bounds changes, not on every viewport tick.
  }, [boundsKey, fitToView]);

  // Pointer-based pan. Right click is reserved for the browser menu, so pan
  // on left/middle button drag. TreeCard stops pointerdown propagation so card
  // clicks don't get treated as a pan start.
  const handlePointerDown = (e: ReactPointerEvent<HTMLDivElement>) => {
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
  const handlePointerMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    const pan = panStateRef.current;
    if (pan?.pointerId !== e.pointerId) return;
    setViewport({
      x: pan.startVx + (e.clientX - pan.startX),
      y: pan.startVy + (e.clientY - pan.startY),
      scale: viewport.scale
    });
  };
  const endPan = (e: ReactPointerEvent<HTMLDivElement>) => {
    const pan = panStateRef.current;
    if (pan?.pointerId !== e.pointerId) return;
    e.currentTarget.releasePointerCapture(e.pointerId);
    panStateRef.current = null;
    setIsPanning(false);
  };

  const handleWheel = (e: ReactWheelEvent<HTMLDivElement>) => {
    const stage = stageRef.current;
    if (!stage) return;
    if (e.ctrlKey || e.metaKey) {
      e.preventDefault();
      const rect = stage.getBoundingClientRect();
      const sx = e.clientX - rect.left;
      const sy = e.clientY - rect.top;
      const factor = Math.exp(-e.deltaY * 0.002);
      const ns = clamp(viewport.scale * factor, MIN_SCALE, MAX_SCALE);
      const wx = (sx - viewport.x) / viewport.scale;
      const wy = (sy - viewport.y) / viewport.scale;
      setViewport({ scale: ns, x: sx - wx * ns, y: sy - wy * ns });
      return;
    }
    setViewport((p) => ({ ...p, x: p.x - e.deltaX, y: p.y - e.deltaY }));
  };

  const handleZoomIn = useCallback(() => {
    setViewport((p) => ({
      ...p,
      scale: clamp(p.scale * 1.15, MIN_SCALE, MAX_SCALE)
    }));
  }, []);
  const handleZoomOut = useCallback(() => {
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
  }, []);

  const openIntent = useCallback(
    (id: string) => {
      const target =
        search.length > 0 ? `/intents/${id}?${search}` : `/intents/${id}`;
      void navigate(target);
    },
    [navigate, search]
  );

  const handleCreated = useCallback(
    (intent: { id: string }) => {
      openIntent(intent.id);
    },
    [openIntent]
  );

  return (
    <section
      className="relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden border-base-300 bg-base-200 max-md:border-b md:border-r"
      aria-label="Дерево Intents"
    >
      <div className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-base-300 bg-base-100 px-3.5 py-3">
        <h2 className="m-0 truncate text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          {contextTitle(context)}
        </h2>
        <div className="flex items-center gap-2">
          {state.kind === "ready" ? (
            <span className="text-[11px] tabular-nums text-base-content/50">
              {String(state.model.nodes.length)} интентов ·{" "}
              {String(edges.length)} связей
            </span>
          ) : null}
          <CreateIntentButton onCreated={handleCreated} />
        </div>
      </div>

      <div
        ref={stageRef}
        data-empty="1"
        className="relative min-h-0 flex-1 overflow-hidden bg-base-200 select-none"
        style={{ cursor: isPanning ? "grabbing" : "grab" }}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={endPan}
        onPointerCancel={endPan}
        onWheel={handleWheel}
      >
        {/* Dot grid */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0"
          style={{
            backgroundImage:
              "radial-gradient(circle, var(--color-base-300, oklch(0.93 0.005 250)) 1px, transparent 1px)",
            backgroundSize: `${String(24 * viewport.scale)}px ${String(24 * viewport.scale)}px`,
            backgroundPosition: `${String(viewport.x)}px ${String(viewport.y)}px`,
            opacity: 0.6
          }}
        />

        {state.kind === "loading" ? (
          <p className="absolute left-1/2 top-1/2 m-0 -translate-x-1/2 -translate-y-1/2 text-[13px] text-base-content/60">
            Загрузка дерева…
          </p>
        ) : null}
        {state.kind === "error" ? (
          <p
            role="alert"
            className="absolute left-1/2 top-1/2 m-0 -translate-x-1/2 -translate-y-1/2 text-[13px] text-base-content/60"
          >
            {state.message}
          </p>
        ) : null}
        {state.kind === "ready" && state.model.nodes.length === 0 ? (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
            <p className="m-0 text-[13px] text-base-content/60">
              В этом контексте пока пусто.
            </p>
          </div>
        ) : null}

        {state.kind === "ready" && state.model.nodes.length > 0 ? (
          <div
            className="absolute left-0 top-0 origin-top-left"
            style={{
              transform: `translate(${String(viewport.x)}px, ${String(viewport.y)}px) scale(${String(viewport.scale)})`,
              width: state.model.bounds.w,
              height: state.model.bounds.h
            }}
          >
            <EdgeLayer
              edges={edges}
              positions={state.model.positions}
              relatedIds={relatedIds}
              width={state.model.bounds.w}
              height={state.model.bounds.h}
            />
            {state.model.nodes.map((node) => {
              const pos = state.model.positions.get(node.id);
              if (!pos) return null;
              return (
                <TreeCard
                  key={node.id}
                  intent={node.intent}
                  pos={pos}
                  active={node.id === activeId}
                  dim={relatedIds !== null && !relatedIds.has(node.id)}
                  onSelect={openIntent}
                />
              );
            })}
          </div>
        ) : null}

        {/* Bottom-left hint pill */}
        <div className="pointer-events-none absolute bottom-3 left-3 flex items-center gap-1.5 rounded-full border border-base-300 bg-base-100/90 px-2.5 py-1 text-[11px] text-base-content/60 shadow-sm">
          <Move aria-hidden size={11} strokeWidth={2} />
          Тяните канвас · клик по карточке открывает интент · ⌘/Ctrl + колесо —
          zoom
        </div>

        {/* Bottom-right zoom toolbar */}
        <div className="pointer-events-none absolute bottom-3 right-3">
          <ZoomToolbar
            scale={viewport.scale}
            onZoomIn={handleZoomIn}
            onZoomOut={handleZoomOut}
            onFit={fitToView}
          />
        </div>
      </div>
    </section>
  );
}
