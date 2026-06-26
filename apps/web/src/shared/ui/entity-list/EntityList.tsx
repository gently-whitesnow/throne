import { useState, type DragEvent, type ReactNode } from "react";

import { EntityListRowContent } from "./EntityListRow";

/**
 * Custom MIME used to mark drags of intent rows from this list. Foreign drop
 * targets (например, секция «Связи» или Pinned в сайдбаре) сниффят его в
 * dragover через `dataTransfer.types`, чтобы подсветить drop-зону без чтения
 * payload.
 */
export const INTENT_DND_MIME = "application/x-throne-intent-id";

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
  /** When true, renders a pin marker next to the title. Bookmarks the row in some context. */
  pinned?: boolean;
  /** When true, renders a live-session dot — the intent has a running terminal session. */
  live?: boolean;
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
    e.dataTransfer.setData(INTENT_DND_MIME, id);
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
            <EntityListRowContent row={row} />
          </li>
        );
      })}
    </ul>
  );
}
