import type React from "react";

interface ResizeHandleProps {
  ariaLabel: string;
  onPointerDown: (event: React.PointerEvent<HTMLDivElement>) => void;
}

/** Вертикальный col-resize разделитель между панелями. */
export function ResizeHandle({ ariaLabel, onPointerDown }: ResizeHandleProps) {
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
