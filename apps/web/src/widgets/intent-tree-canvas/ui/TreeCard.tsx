import { Pin } from "lucide-react";
import { memo, type MouseEvent, type PointerEvent } from "react";

import { intentStatusMeta, type IntentListItem } from "@/entities/intent";

import { CARD_H, CARD_W, type LayoutPosition } from "../model/layout";

interface TreeCardProps {
  intent: IntentListItem;
  pos: LayoutPosition;
  active: boolean;
  dim: boolean;
  onSelect: (id: string) => void;
}

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}

function TreeCardImpl({ intent, pos, active, dim, onSelect }: TreeCardProps) {
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
        "absolute flex items-stretch overflow-hidden rounded-lg bg-base-100 text-left transition-opacity",
        "border focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary/60",
        active
          ? "border-primary shadow-[0_0_0_3px_oklch(0.5_0.2_255/0.15)]"
          : "border-base-300 hover:border-base-content/30",
        dim ? "opacity-30" : "opacity-100"
      ].join(" ")}
      style={{
        left: pos.x,
        top: pos.y,
        width: CARD_W,
        height: CARD_H
      }}
    >
      {/* Status strip */}
      <span
        className="flex w-[68px] shrink-0 flex-col items-start justify-center border-r border-base-300 px-2 py-2"
        style={{ background: status.surface, color: status.ink }}
      >
        <span className="truncate text-[10px] font-semibold uppercase tracking-wide">
          {status.label}
        </span>
      </span>

      {/* Body */}
      <span className="flex min-w-0 flex-1 flex-col justify-between px-3 py-2">
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
        <span className="flex items-center gap-1.5 truncate">
          {intent.pinned_in.length > 0 ? (
            <Pin
              aria-hidden
              size={11}
              strokeWidth={2}
              className="shrink-0 text-base-content/50"
            />
          ) : null}
          {intent.tags.slice(0, 3).map((t) => (
            <span
              key={t.id}
              className="truncate text-[10px] text-base-content/60"
            >
              #{t.name}
            </span>
          ))}
        </span>
      </span>

      {/* Version */}
      <span className="flex w-[44px] shrink-0 items-start justify-end px-2 pt-2">
        <span className="text-[11px] tabular-nums text-base-content/50">
          v{String(intent.current_version)}
        </span>
      </span>
    </button>
  );
}

export const TreeCard = memo(TreeCardImpl);
