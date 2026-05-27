import { Pin } from "lucide-react";
import { memo, type MouseEvent, type PointerEvent } from "react";

import { intentStatusMeta } from "@/entities/intent";

import { CARD_H, CARD_W, type LayoutPosition } from "../model/layout";
import type { CanvasCardIntent } from "../model/tree-data";

interface TreeCardProps {
  intent: CanvasCardIntent;
  pos: LayoutPosition;
  active: boolean;
  /** True when a selection exists and this card is part of its ancestor/descendant chain. */
  related: boolean;
  /** True when a selection exists and this card is *not* in its related set. */
  unrelated: boolean;
  /** Resolved (done) neighbour shown via the «show resolved» toggle — rendered as a muted «ghost». */
  resolved: boolean;
  onSelect: (id: string) => void;
}

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}

function TreeCardImpl({
  intent,
  pos,
  active,
  related,
  unrelated,
  resolved,
  onSelect
}: TreeCardProps) {
  const status = intentStatusMeta[intent.status];
  const title = firstLine(intent.text_short) || intent.id;

  const handleClick = (e: MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
    onSelect(intent.id);
  };
  const stopPropagation = (e: PointerEvent<HTMLButtonElement>) => {
    e.stopPropagation();
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      onPointerDown={stopPropagation}
      aria-current={active ? "true" : undefined}
      aria-label={`Открыть intent ${title}`}
      className={[
        "absolute flex flex-col overflow-hidden rounded-lg bg-base-100 text-left",
        "focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary/60",
        active
          ? "border-2 border-primary shadow-[0_0_0_4px_oklch(0.5_0.2_255/0.28)]"
          : related
            ? "border border-primary/60"
            : unrelated
              ? "border border-base-300/70 hover:border-base-content/30"
              : "border border-base-300 hover:border-base-content/30",
        // Resolved (done) ghosts read as quiet background context: dashed border
        // and muted opacity, unless the card is the active selection.
        resolved && !active ? "border-dashed opacity-70" : ""
      ].join(" ")}
      style={{
        left: pos.x,
        top: pos.y,
        width: CARD_W,
        height: CARD_H
      }}
    >
      {/* Header: status chip on the left, version on the right. */}
      <span
        className="flex items-center justify-between gap-2 border-b border-base-300/70 px-2 py-1"
        style={{ background: status.surface, color: status.ink }}
      >
        <span className="truncate text-[10px] font-semibold uppercase tracking-wide">
          {status.label}
        </span>
        <span className="shrink-0 text-[10px] tabular-nums opacity-70">
          v{String(intent.current_version)}
        </span>
      </span>

      {/* Body: title + tags row. The title clamps to two lines and the tags
          row sits at the bottom edge of the card. */}
      <span className="flex min-h-0 flex-1 flex-col justify-between gap-1 px-3 py-2">
        <span
          className="overflow-hidden text-[12.5px] font-medium leading-snug text-base-content"
          style={{
            display: "-webkit-box",
            WebkitLineClamp: 2,
            WebkitBoxOrient: "vertical"
          }}
        >
          {title}
        </span>
        <span className="flex items-center gap-1.5 truncate text-[10px] text-base-content/60">
          {intent.pinned_in.length > 0 ? (
            <Pin
              aria-hidden
              size={11}
              strokeWidth={2}
              className="shrink-0 text-base-content/50"
            />
          ) : null}
          {intent.tags.slice(0, 3).map((t) => (
            <span key={t.id} className="truncate">
              #{t.name}
            </span>
          ))}
        </span>
      </span>
    </button>
  );
}

export const TreeCard = memo(TreeCardImpl);
