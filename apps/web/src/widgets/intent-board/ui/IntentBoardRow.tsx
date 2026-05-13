import { type DragEvent, type ReactNode } from "react";
import { NavLink } from "react-router-dom";

import type { EntityListRow } from "@/shared/ui";

export interface BoardRowDnd {
  draggable?: boolean;
  isDragging?: boolean;
  showLineBefore?: boolean;
  showLineAfter?: boolean;
  onDragStart?: (e: DragEvent<HTMLLIElement>) => void;
  onDragOver?: (e: DragEvent<HTMLLIElement>) => void;
  onDrop?: (e: DragEvent<HTMLLIElement>) => void;
  onDragEnd?: (e: DragEvent<HTMLLIElement>) => void;
}

interface IntentBoardRowProps {
  row: EntityListRow;
  innerRef?: (el: HTMLLIElement | null) => void;
  onHover?: (id: string | null) => void;
  dnd?: BoardRowDnd;
  /** Optional content rendered after the warning chip (e.g. DAG-parent chips). */
  trailing?: ReactNode;
}

/**
 * Presentational row used both as a top-level singleton and as a cluster
 * member. Mirrors the visual contract of EntityList rows so the board reads
 * the same density and active-state highlight regardless of grouping.
 *
 * DnD handlers are passed in by the host — this component is intentionally
 * dumb about whether the drop will be a card move, a cluster move, or an
 * insert into a cluster.
 */
export function IntentBoardRow({
  row,
  innerRef,
  onHover,
  dnd,
  trailing
}: IntentBoardRowProps) {
  const draggable = dnd?.draggable ?? false;
  return (
    <li
      ref={(el) => {
        innerRef?.(el);
      }}
      draggable={draggable}
      onDragStart={dnd?.onDragStart}
      onDragOver={dnd?.onDragOver}
      onDrop={dnd?.onDrop}
      onDragEnd={dnd?.onDragEnd}
      onMouseEnter={
        onHover
          ? () => {
              onHover(row.id);
            }
          : undefined
      }
      onMouseLeave={
        onHover
          ? () => {
              onHover(null);
            }
          : undefined
      }
      className={[
        "relative",
        row.outline ?? "",
        dnd?.isDragging ? "opacity-50" : "",
        dnd?.showLineBefore
          ? "before:absolute before:left-0 before:right-0 before:top-0 before:h-px before:bg-primary"
          : "",
        dnd?.showLineAfter
          ? "after:absolute after:left-0 after:right-0 after:bottom-0 after:h-px after:bg-primary"
          : ""
      ]
        .filter(Boolean)
        .join(" ")}
    >
      <NavLink
        to={row.href}
        className={({ isActive }) =>
          [
            "flex min-h-9 items-center gap-2 border-l-2 px-3.5 py-1.5 text-[13px] no-underline focus-visible:outline-2 focus-visible:outline-primary focus-visible:-outline-offset-2",
            isActive
              ? "border-primary bg-primary/10 text-base-content"
              : "border-transparent text-base-content hover:bg-base-200"
          ].join(" ")
        }
      >
        {row.badge ? (
          <span
            className="inline-flex h-[18px] flex-shrink-0 items-center rounded px-1.5 text-[10px] font-semibold"
            style={
              row.badgeColor || row.badgeTextColor
                ? { background: row.badgeColor, color: row.badgeTextColor }
                : { background: "var(--color-base-200)" }
            }
          >
            {row.badge}
          </span>
        ) : null}
        <span className="flex min-w-0 flex-1 flex-col gap-px">
          <span className="truncate font-medium leading-tight">
            {row.title}
          </span>
          {row.subtitle ? (
            <span className="truncate text-[11px] text-base-content/60">
              {row.subtitle}
            </span>
          ) : null}
        </span>
        {row.meta ? (
          <span className="flex-shrink-0 text-[11px] tabular-nums text-base-content/60">
            {row.meta}
          </span>
        ) : null}
        {row.warning ? (
          <span
            className="flex-shrink-0 rounded border border-warning/40 bg-warning/10 px-1.5 py-px text-[10px] font-semibold text-warning"
            title={row.warningTitle ?? row.warning}
          >
            {row.warning}
          </span>
        ) : null}
        {trailing ?? row.trailing ?? null}
      </NavLink>
      {row.tint ? (
        <span
          aria-hidden
          className="pointer-events-none absolute bottom-0 left-0 top-0 w-[3px]"
          style={{ backgroundColor: row.tint }}
        />
      ) : null}
    </li>
  );
}
