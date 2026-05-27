import { useVirtualizer, type VirtualItem } from "@tanstack/react-virtual";
import { useEffect, useState, type DragEvent, type RefObject } from "react";

import {
  INTENT_DND_MIME,
  type EntityListReorder,
  type EntityListRow
} from "./EntityList";
import { EntityListRowContent } from "./EntityListRow";

interface VirtualEntityListProps {
  items: readonly EntityListRow[];
  emptyMessage?: string;
  /** Element that scrolls the list — virtualizer measures viewport from it. */
  scrollParentRef: RefObject<HTMLElement | null>;
  /** Pixel estimate per row (real heights are measured after mount). */
  estimateSize?: number;
  /** Rows to render outside the viewport on either side. */
  overscan?: number;
  /** Called once the last visible index reaches the tail — for cursor pagination. */
  onReachEnd?: () => void;
  /** When set, rows become draggable and this fires after a successful drop. */
  onReorder?: (move: EntityListReorder) => void;
  /** Fires when the pointer enters/leaves a row (enter delivers row id; leave delivers null). */
  onRowHover?: (id: string | null) => void;
  /** Receives the live `<li>` element for each row; lets the host position overlays. */
  rowRef?: (id: string, el: HTMLLIElement | null) => void;
  /** Bumps whenever the host needs to know which rows are currently in the viewport. */
  onVisibleItemsChange?: (items: readonly VirtualItem[]) => void;
}

/**
 * Виртуализованный аналог `EntityList`. Рендерит только видимое окно через
 * `@tanstack/react-virtual`, сохраняя DnD/hover/rowRef-контракт оригинала.
 *
 * `scrollParentRef` обязателен: вирту нужен фактический scroll-контейнер, чтобы
 * считать viewport и обновляться на скролле; без него все строки попадут в кадр
 * (overscan=Infinity), теряя смысл виртуализации.
 */
export function VirtualEntityList({
  items,
  emptyMessage,
  scrollParentRef,
  estimateSize = 40,
  overscan = 8,
  onReachEnd,
  onReorder,
  onRowHover,
  rowRef,
  onVisibleItemsChange
}: VirtualEntityListProps) {
  const [draggingId, setDraggingId] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<{
    id: string;
    position: "before" | "after";
  } | null>(null);

  const virtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => scrollParentRef.current,
    estimateSize: () => estimateSize,
    overscan
  });

  const virtualItems = virtualizer.getVirtualItems();
  const totalSize = virtualizer.getTotalSize();

  // Сообщаем хосту, какие строки сейчас видны — нужно для useLinksSummary
  // и подобных «зависим только от viewport» запросов.
  useEffect(() => {
    onVisibleItemsChange?.(virtualItems);
  }, [onVisibleItemsChange, virtualItems]);

  // Сентинел подгрузки: достигли хвоста — просим следующую страницу.
  useEffect(() => {
    if (!onReachEnd || virtualItems.length === 0) return;
    const last = virtualItems[virtualItems.length - 1];
    if (last.index >= items.length - 1) onReachEnd();
  }, [items.length, onReachEnd, virtualItems]);

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
    <ul
      className="m-0 flex list-none flex-col p-0"
      role="list"
      style={{ height: totalSize, position: "relative" }}
    >
      {virtualItems.map((virtualRow) => {
        if (virtualRow.index >= items.length) return null;
        const row = items[virtualRow.index];
        const isDragging = draggingId === row.id;
        const showLineBefore =
          dropTarget?.id === row.id && dropTarget.position === "before";
        const showLineAfter =
          dropTarget?.id === row.id && dropTarget.position === "after";
        return (
          <li
            key={row.id}
            data-index={virtualRow.index}
            ref={(el) => {
              virtualizer.measureElement(el);
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
              "absolute left-0 top-0 w-full",
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
            style={{ transform: `translateY(${String(virtualRow.start)}px)` }}
          >
            <EntityListRowContent row={row} />
          </li>
        );
      })}
    </ul>
  );
}
