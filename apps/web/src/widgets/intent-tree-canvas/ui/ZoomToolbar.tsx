import { Maximize2, Minus, Plus, Waypoints } from "lucide-react";

interface ZoomToolbarProps {
  scale: number;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onFit: () => void;
  /** Whether resolved (done) related nodes are currently shown on the canvas. */
  showResolved: boolean;
  onToggleResolved: () => void;
}

export function ZoomToolbar({
  scale,
  onZoomIn,
  onZoomOut,
  onFit,
  showResolved,
  onToggleResolved
}: ZoomToolbarProps) {
  return (
    <div
      // Канвас-сцена на pointerdown делает setPointerCapture (пан), из-за чего
      // click перенаправляется на сцену и onClick кнопок не срабатывает. Гасим
      // pointerdown здесь — так же, как TreeCard, — чтобы клики по тулбару жили.
      onPointerDown={(e) => {
        e.stopPropagation();
      }}
      className="pointer-events-auto flex items-center gap-0.5 rounded-md border border-base-300 bg-base-100/90 px-1 py-1 shadow-sm backdrop-blur"
      role="toolbar"
      aria-label="Управление канвасом"
    >
      <button
        type="button"
        onClick={onToggleResolved}
        aria-pressed={showResolved}
        aria-label="Показать решённые связи"
        title="Показать решённые связи"
        className={[
          "flex h-7 w-7 items-center justify-center rounded",
          showResolved
            ? "bg-primary/10 text-primary"
            : "text-base-content/60 hover:bg-base-200 hover:text-base-content"
        ].join(" ")}
      >
        <Waypoints aria-hidden size={13} strokeWidth={2} />
      </button>
      <span aria-hidden className="mx-0.5 h-4 w-px bg-base-300" />
      <button
        type="button"
        onClick={onZoomOut}
        aria-label="Уменьшить масштаб"
        className="flex h-7 w-7 items-center justify-center rounded text-base-content/60 hover:bg-base-200 hover:text-base-content"
      >
        <Minus aria-hidden size={13} strokeWidth={2} />
      </button>
      <span className="w-10 text-center text-[11px] tabular-nums text-base-content/60">
        {String(Math.round(scale * 100))}%
      </span>
      <button
        type="button"
        onClick={onZoomIn}
        aria-label="Увеличить масштаб"
        className="flex h-7 w-7 items-center justify-center rounded text-base-content/60 hover:bg-base-200 hover:text-base-content"
      >
        <Plus aria-hidden size={13} strokeWidth={2} />
      </button>
      <button
        type="button"
        onClick={onFit}
        aria-label="Вписать в экран"
        title="Вписать в экран"
        className="ml-0.5 flex h-7 w-7 items-center justify-center rounded text-base-content/60 hover:bg-base-200 hover:text-base-content"
      >
        <Maximize2 aria-hidden size={13} strokeWidth={2} />
      </button>
    </div>
  );
}
