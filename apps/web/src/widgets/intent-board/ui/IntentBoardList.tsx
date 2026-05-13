import type { ReactNode } from "react";

import type { EntityListReorder, EntityListRow } from "@/shared/ui";

import type { BoardEntry, ClusterMove } from "../model/board-dnd";
import { useBoardDnd, type BoardDndHandlers } from "../model/useBoardDnd";
import type { ClusterInfo, ClustersResult } from "../model/useClusters";
import { IntentBoardRow } from "./IntentBoardRow";
import { IntentClusterCard } from "./IntentClusterCard";

interface IntentBoardListProps {
  entries: readonly BoardEntry[];
  rowsById: ReadonlyMap<string, EntityListRow>;
  clusters: ClustersResult["clusters"];
  collapsedClusters: (id: string) => boolean;
  toggleCluster: (id: string) => void;
  onCardReorder: (move: EntityListReorder) => void;
  onClusterReorder: (move: ClusterMove) => void;
  onRowHover: (id: string | null) => void;
  rowRef: (id: string, el: HTMLLIElement | null) => void;
  trailingForRow: (id: string) => ReactNode;
  emptyMessage?: string;
}

/**
 * Top-level renderer for the intent board. Walks `entries` in order, emitting
 * a singleton row or a bento cluster card for each. All DnD state lives in
 * `useBoardDnd` — the renderer just maps it into row/header visual props.
 */
export function IntentBoardList({
  entries,
  rowsById,
  clusters,
  collapsedClusters,
  toggleCluster,
  onCardReorder,
  onClusterReorder,
  onRowHover,
  rowRef,
  trailingForRow,
  emptyMessage
}: IntentBoardListProps) {
  const { state, handlers } = useBoardDnd({
    entries,
    clusters,
    onCardReorder,
    onClusterReorder
  });

  if (entries.length === 0) {
    return (
      <p className="px-3.5 py-4 text-[13px] text-base-content/60">
        {emptyMessage ?? "Список пуст."}
      </p>
    );
  }

  return (
    <ul className="flex flex-col py-1" role="list">
      {entries.map((entry) => {
        if (entry.kind === "single") {
          const row = rowsById.get(entry.anchorId);
          if (!row) return null;
          return (
            <SingletonRow
              key={`s:${entry.anchorId}`}
              row={row}
              state={state}
              handlers={handlers}
              onRowHover={onRowHover}
              rowRef={rowRef}
              trailing={trailingForRow(entry.anchorId)}
            />
          );
        }
        const cluster = clusters.get(entry.anchorId);
        if (!cluster) return null;
        return (
          <ClusterEntry
            key={`c:${entry.anchorId}`}
            cluster={cluster}
            rowsById={rowsById}
            collapsed={collapsedClusters(entry.anchorId)}
            onToggle={() => {
              toggleCluster(entry.anchorId);
            }}
            state={state}
            handlers={handlers}
            onRowHover={onRowHover}
            rowRef={rowRef}
            trailingForRow={trailingForRow}
          />
        );
      })}
    </ul>
  );
}

interface SingletonRowProps {
  row: EntityListRow;
  state: ReturnType<typeof useBoardDnd>["state"];
  handlers: BoardDndHandlers;
  onRowHover: (id: string | null) => void;
  rowRef: (id: string, el: HTMLLIElement | null) => void;
  trailing: ReactNode;
}

function SingletonRow({
  row,
  state,
  handlers,
  onRowHover,
  rowRef,
  trailing
}: SingletonRowProps) {
  const { dragOp, drop, isCardMode, isClusterMode } = state;
  const id = row.id;
  const dragging = isCardMode && dragOp?.id === id;
  const targetHere = drop !== null && drop.overId === id;
  return (
    <IntentBoardRow
      row={row}
      innerRef={(el) => {
        rowRef(id, el);
      }}
      onHover={onRowHover}
      trailing={trailing}
      dnd={{
        draggable: true,
        isDragging: dragging,
        showLineBefore: targetHere && drop.position === "before",
        showLineAfter: targetHere && drop.position === "after",
        onDragStart: (e) => {
          handlers.startCard(e, id);
        },
        onDragOver: (e) => {
          if (isCardMode) handlers.cardOver(e, id);
          else if (isClusterMode) handlers.clusterOver(e, id);
        },
        onDrop: (e) => {
          if (isCardMode) handlers.cardDrop(e, id);
          else if (isClusterMode) handlers.clusterDrop(e, id);
        },
        onDragEnd: handlers.cancelDrag
      }}
    />
  );
}

interface ClusterEntryProps {
  cluster: ClusterInfo;
  rowsById: ReadonlyMap<string, EntityListRow>;
  collapsed: boolean;
  onToggle: () => void;
  state: ReturnType<typeof useBoardDnd>["state"];
  handlers: BoardDndHandlers;
  onRowHover: (id: string | null) => void;
  rowRef: (id: string, el: HTMLLIElement | null) => void;
  trailingForRow: (id: string) => ReactNode;
}

function ClusterEntry({
  cluster,
  rowsById,
  collapsed,
  onToggle,
  state,
  handlers,
  onRowHover,
  rowRef,
  trailingForRow
}: ClusterEntryProps) {
  const { dragOp, drop, isCardMode, isClusterMode } = state;
  const id = cluster.clusterId;
  const dragging = isClusterMode && dragOp?.id === id;
  const targetHere = isClusterMode && drop !== null && drop.overId === id;
  return (
    <IntentClusterCard
      clusterId={id}
      memberCount={cluster.memberIds.length}
      commonTags={cluster.commonTags}
      collapsed={collapsed}
      onToggle={onToggle}
      dnd={{
        draggable: true,
        isDragging: dragging,
        showLineBefore: targetHere && drop.position === "before",
        showLineAfter: targetHere && drop.position === "after",
        onDragStart: (e) => {
          handlers.startCluster(e, id);
        },
        onDragOver: (e) => {
          handlers.clusterOver(e, id);
        },
        onDrop: (e) => {
          handlers.clusterDrop(e, id);
        },
        onDragEnd: handlers.cancelDrag
      }}
    >
      {cluster.memberIds.map((memberId) => {
        const row = rowsById.get(memberId);
        if (!row) return null;
        return (
          <IntentBoardRow
            key={memberId}
            row={row}
            innerRef={(el) => {
              rowRef(memberId, el);
            }}
            onHover={onRowHover}
            trailing={trailingForRow(memberId)}
            dnd={{
              draggable: true,
              isDragging: isCardMode && dragOp?.id === memberId,
              showLineBefore:
                isCardMode &&
                drop?.overId === memberId &&
                drop.position === "before",
              showLineAfter:
                isCardMode &&
                drop?.overId === memberId &&
                drop.position === "after",
              onDragStart: (e) => {
                handlers.startCard(e, memberId);
              },
              onDragOver: (e) => {
                handlers.cardOver(e, memberId);
              },
              onDrop: (e) => {
                handlers.cardDrop(e, memberId);
              },
              onDragEnd: handlers.cancelDrag
            }}
          />
        );
      })}
    </IntentClusterCard>
  );
}
