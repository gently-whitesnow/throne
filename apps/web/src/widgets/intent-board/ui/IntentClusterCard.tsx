import { ChevronDown, ChevronRight, GripVertical } from "lucide-react";
import { type DragEvent, type ReactNode } from "react";

const STICKY_THRESHOLD = 5;

export interface ClusterCardDnd {
  draggable: boolean;
  isDragging: boolean;
  showLineBefore: boolean;
  showLineAfter: boolean;
  onDragStart: (e: DragEvent<HTMLDivElement>) => void;
  onDragOver: (e: DragEvent<HTMLDivElement>) => void;
  onDrop: (e: DragEvent<HTMLDivElement>) => void;
  onDragEnd: (e: DragEvent<HTMLDivElement>) => void;
}

interface IntentClusterCardProps {
  clusterId: string;
  memberCount: number;
  commonTags: readonly string[];
  collapsed: boolean;
  onToggle: () => void;
  dnd: ClusterCardDnd;
  children: ReactNode;
}

/**
 * Bento wrapper for a cluster of linked intents. Header carries the cluster
 * grip (drag-the-whole-thing), a count, the shared tag (if any), and the
 * collapse toggle. When the cluster grows past STICKY_THRESHOLD members and
 * is expanded the header sticks so the user keeps context while scrolling
 * through it.
 *
 * Intra-cluster card DnD is handled by the children — the host wires it.
 */
export function IntentClusterCard({
  memberCount,
  commonTags,
  collapsed,
  onToggle,
  dnd,
  children
}: IntentClusterCardProps) {
  const sticky = !collapsed && memberCount > STICKY_THRESHOLD;
  return (
    <div
      className={[
        "relative mx-2 my-1.5 rounded-md border border-base-300 bg-base-100/50",
        dnd.isDragging ? "opacity-50" : "",
        dnd.showLineBefore
          ? "before:pointer-events-none before:absolute before:left-0 before:right-0 before:top-[-4px] before:h-px before:bg-primary"
          : "",
        dnd.showLineAfter
          ? "after:pointer-events-none after:absolute after:left-0 after:right-0 after:bottom-[-4px] after:h-px after:bg-primary"
          : ""
      ]
        .filter(Boolean)
        .join(" ")}
      onDragOver={dnd.onDragOver}
      onDrop={dnd.onDrop}
    >
      <div
        draggable={dnd.draggable}
        onDragStart={dnd.onDragStart}
        onDragEnd={dnd.onDragEnd}
        className={[
          "flex items-center gap-1.5 rounded-t-md border-b border-base-300 bg-base-200/60 px-2 py-1 text-[11px] uppercase tracking-wider text-base-content/70",
          sticky ? "sticky top-0 z-20" : "",
          dnd.draggable ? "cursor-grab active:cursor-grabbing" : ""
        ]
          .filter(Boolean)
          .join(" ")}
      >
        <GripVertical
          aria-hidden
          size={12}
          strokeWidth={2}
          className="flex-shrink-0 text-base-content/50"
        />
        <button
          type="button"
          onClick={onToggle}
          aria-expanded={!collapsed}
          aria-label={collapsed ? "Развернуть кластер" : "Свернуть кластер"}
          className="flex flex-shrink-0 items-center text-base-content/60 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary"
        >
          {collapsed ? (
            <ChevronRight size={14} strokeWidth={2} />
          ) : (
            <ChevronDown size={14} strokeWidth={2} />
          )}
        </button>
        <span className="flex-shrink-0 font-semibold">Кластер</span>
        <span className="flex-shrink-0 text-base-content/50">·</span>
        <span className="flex-shrink-0 tabular-nums">{memberCount}</span>
        {commonTags.length > 0 ? (
          <>
            <span className="flex-shrink-0 text-base-content/50">·</span>
            <span
              className="truncate text-base-content/60"
              title={commonTags.map((t) => `#${t}`).join(" ")}
            >
              #{commonTags[0]}
              {commonTags.length > 1
                ? ` +${String(commonTags.length - 1)}`
                : ""}
            </span>
          </>
        ) : null}
      </div>
      {!collapsed ? (
        <ul className="flex flex-col py-0.5" role="list">
          {children}
        </ul>
      ) : null}
    </div>
  );
}
