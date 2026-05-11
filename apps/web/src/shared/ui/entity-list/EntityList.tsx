import { useState, type DragEvent, type ReactNode } from "react";
import { NavLink } from "react-router-dom";

export interface EntityListRow {
  id: string;
  title: string;
  subtitle?: string;
  meta?: string;
  badge?: string;
  badgeColor?: string;
  badgeTextColor?: string;
  href: string;
  /** Optional warning chip rendered after `meta` (e.g. "⚠ blocked by 2"). */
  warning?: string;
  warningTitle?: string;
  /** Optional outline class applied to the row wrapper (used for peer-highlight). */
  outline?: string;
  /** Optional accent colour for a thin left-edge stripe on the row (e.g. family-grouping marker). */
  tint?: string;
  /** Extra inline content rendered after the warning chip (e.g. link badges). */
  trailing?: ReactNode;
}

/**
 * Pivot identifiers describing where a dragged item should land. The list passes
 * these to the host, which is expected to send them to a server move endpoint
 * (e.g. POST /intents/{id}/move) — the server resolves them into a sort key.
 */
export interface EntityListReorder {
  movedId: string;
  beforeId: string | null;
  afterId: string | null;
}

interface EntityListProps {
  items: readonly EntityListRow[];
  emptyMessage?: string;
  /** When set, rows become draggable and this fires after a successful drop. */
  onReorder?: (move: EntityListReorder) => void;
  /** Fires when the pointer enters/leaves a row (enter delivers row id; leave delivers null). */
  onRowHover?: (id: string | null) => void;
  /** Receives the live `<li>` element for each row; lets the host position overlays. */
  rowRef?: (id: string, el: HTMLLIElement | null) => void;
}

export function EntityList({
  items,
  emptyMessage,
  onReorder,
  onRowHover,
  rowRef
}: EntityListProps) {
  const [draggingId, setDraggingId] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<{
    id: string;
    position: "before" | "after";
  } | null>(null);

  if (items.length === 0) {
    return (
      <p className="px-3.5 py-4 text-[13px] text-base-content/60">
        {emptyMessage ?? "Список пуст."}
      </p>
    );
  }

  const draggable = Boolean(onReorder);

  const handleDragStart = (e: DragEvent<HTMLLIElement>, id: string) => {
    setDraggingId(id);
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", id);
  };

  const handleDragOver = (e: DragEvent<HTMLLIElement>, id: string) => {
    if (!draggable || !draggingId || draggingId === id) return;
    e.preventDefault();
    const rect = e.currentTarget.getBoundingClientRect();
    const position =
      e.clientY < rect.top + rect.height / 2 ? "before" : "after";
    setDropTarget((prev) =>
      prev?.id === id && prev.position === position ? prev : { id, position }
    );
  };

  const handleDrop = (e: DragEvent<HTMLLIElement>, id: string) => {
    if (!draggable || !draggingId || draggingId === id) return;
    e.preventDefault();
    const target = dropTarget?.id === id ? dropTarget : null;
    setDraggingId(null);
    setDropTarget(null);
    if (!target) return;

    const targetIndex = items.findIndex((i) => i.id === id);
    if (targetIndex < 0) return;
    // For "before target", neighbours are (targetIndex-1, targetIndex). For "after",
    // they are (targetIndex, targetIndex+1). Skip the moved item if it sits in either slot.
    let beforeIdx =
      target.position === "before" ? targetIndex - 1 : targetIndex;
    let afterIdx = target.position === "before" ? targetIndex : targetIndex + 1;
    if (items[beforeIdx]?.id === draggingId) beforeIdx -= 1;
    if (items[afterIdx]?.id === draggingId) afterIdx += 1;

    onReorder?.({
      movedId: draggingId,
      beforeId: items[beforeIdx]?.id ?? null,
      afterId: items[afterIdx]?.id ?? null
    });
  };

  const handleDragEnd = () => {
    setDraggingId(null);
    setDropTarget(null);
  };

  return (
    <ul className="flex flex-col py-1" role="list">
      {items.map((row) => {
        const isDragging = draggingId === row.id;
        const showLineBefore =
          dropTarget?.id === row.id && dropTarget.position === "before";
        const showLineAfter =
          dropTarget?.id === row.id && dropTarget.position === "after";
        return (
          <li
            key={row.id}
            ref={(el) => {
              rowRef?.(row.id, el);
            }}
            draggable={draggable}
            onDragStart={
              draggable
                ? (e) => {
                    handleDragStart(e, row.id);
                  }
                : undefined
            }
            onDragOver={
              draggable
                ? (e) => {
                    handleDragOver(e, row.id);
                  }
                : undefined
            }
            onDrop={
              draggable
                ? (e) => {
                    handleDrop(e, row.id);
                  }
                : undefined
            }
            onDragEnd={draggable ? handleDragEnd : undefined}
            onMouseEnter={
              onRowHover
                ? () => {
                    onRowHover(row.id);
                  }
                : undefined
            }
            onMouseLeave={
              onRowHover
                ? () => {
                    onRowHover(null);
                  }
                : undefined
            }
            className={[
              "relative",
              row.outline ?? "",
              isDragging ? "opacity-50" : "",
              showLineBefore
                ? "before:absolute before:left-0 before:right-0 before:top-0 before:h-px before:bg-primary"
                : "",
              showLineAfter
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
                      ? {
                          background: row.badgeColor,
                          color: row.badgeTextColor
                        }
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
              {row.trailing ?? null}
            </NavLink>
            {row.tint ? (
              // Тонкая вертикальная полоска у левого края — маркер «семьи»
              // карточек (общий derived_from-предок). Лежит поверх NavLink,
              // чтобы hover-bg её не перекрашивал; pointer-events отключены,
              // чтобы клик уходил на ссылку.
              <span
                aria-hidden
                className="pointer-events-none absolute bottom-0 left-0 top-0 w-[3px]"
                style={{ backgroundColor: row.tint }}
              />
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}
