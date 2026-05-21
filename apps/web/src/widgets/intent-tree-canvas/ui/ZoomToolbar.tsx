import { Maximize2, Minus, Plus } from "lucide-react";

interface ZoomToolbarProps {
  scale: number;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onFit: () => void;
}

export function ZoomToolbar({
  scale,
  onZoomIn,
  onZoomOut,
  onFit
}: ZoomToolbarProps) {
  return (
    <div
      className="pointer-events-auto flex items-center gap-0.5 rounded-md border border-base-300 bg-base-100/90 px-1 py-1 shadow-sm backdrop-blur"
      role="toolbar"
      aria-label="Управление масштабом"
    >
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
