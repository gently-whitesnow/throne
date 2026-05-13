import { useCallback, useState, type DragEvent } from "react";

import type { EntityListReorder } from "@/shared/ui";

import {
  buildFlatIds,
  resolveCardMove,
  resolveClusterMove,
  type BoardEntry,
  type ClusterMove
} from "./board-dnd";
import type { ClustersResult } from "./useClusters";

interface DragOp {
  kind: "card" | "cluster";
  id: string;
}

interface DropTarget {
  /** Intent id (card mode) or top-level entry anchor id (cluster mode). */
  overId: string;
  position: "before" | "after";
}

export interface BoardDndState {
  dragOp: DragOp | null;
  drop: DropTarget | null;
  isCardMode: boolean;
  isClusterMode: boolean;
}

export interface BoardDndHandlers {
  startCard: (e: DragEvent, id: string) => void;
  startCluster: (e: DragEvent, id: string) => void;
  cardOver: (e: DragEvent<HTMLLIElement>, id: string) => void;
  cardDrop: (e: DragEvent<HTMLLIElement>, id: string) => void;
  clusterOver: (e: DragEvent<HTMLElement>, anchor: string) => void;
  clusterDrop: (e: DragEvent<HTMLElement>, anchor: string) => void;
  cancelDrag: () => void;
}

/**
 * State machine for the dual-scope intent board DnD: card-level (any intent
 * card ↔ any flat position) and cluster-level (whole cluster ↔ between
 * top-level entries). Both share `dragOp` / `drop` state so the renderer can
 * highlight either a row indicator or a cluster boundary, never both.
 */
export function useBoardDnd({
  entries,
  clusters,
  onCardReorder,
  onClusterReorder
}: {
  entries: readonly BoardEntry[];
  clusters: ClustersResult["clusters"];
  onCardReorder: (move: EntityListReorder) => void;
  onClusterReorder: (move: ClusterMove) => void;
}): { state: BoardDndState; handlers: BoardDndHandlers } {
  const [dragOp, setDragOp] = useState<DragOp | null>(null);
  const [drop, setDrop] = useState<DropTarget | null>(null);

  const cancelDrag = useCallback(() => {
    setDragOp(null);
    setDrop(null);
  }, []);

  const startCard = useCallback((e: DragEvent, id: string) => {
    setDragOp({ kind: "card", id });
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", `card:${id}`);
  }, []);
  const startCluster = useCallback((e: DragEvent, id: string) => {
    setDragOp({ kind: "cluster", id });
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", `cluster:${id}`);
  }, []);

  const updateDrop = useCallback(
    (e: DragEvent, el: HTMLElement, id: string) => {
      const rect = el.getBoundingClientRect();
      const side: "before" | "after" =
        e.clientY < rect.top + rect.height / 2 ? "before" : "after";
      setDrop((prev) =>
        prev?.overId === id && prev.position === side
          ? prev
          : { overId: id, position: side }
      );
    },
    []
  );

  const cardOver = useCallback(
    (e: DragEvent<HTMLLIElement>, id: string) => {
      if (dragOp?.kind !== "card" || dragOp.id === id) return;
      e.preventDefault();
      e.stopPropagation();
      updateDrop(e, e.currentTarget, id);
    },
    [dragOp, updateDrop]
  );

  const cardDrop = useCallback(
    (e: DragEvent<HTMLLIElement>, id: string) => {
      if (dragOp?.kind !== "card" || dragOp.id === id) {
        cancelDrag();
        return;
      }
      e.preventDefault();
      e.stopPropagation();
      const target = drop?.overId === id ? drop : null;
      const moved = dragOp.id;
      cancelDrag();
      if (!target) return;
      const flatIds = buildFlatIds(entries, clusters);
      onCardReorder(resolveCardMove(moved, id, target.position, flatIds));
    },
    [cancelDrag, clusters, dragOp, drop, entries, onCardReorder]
  );

  const clusterOver = useCallback(
    (e: DragEvent<HTMLElement>, anchor: string) => {
      if (dragOp?.kind !== "cluster" || dragOp.id === anchor) return;
      e.preventDefault();
      updateDrop(e, e.currentTarget, anchor);
    },
    [dragOp, updateDrop]
  );

  const clusterDrop = useCallback(
    (e: DragEvent<HTMLElement>, anchor: string) => {
      if (dragOp?.kind !== "cluster" || dragOp.id === anchor) {
        cancelDrag();
        return;
      }
      e.preventDefault();
      const target = drop?.overId === anchor ? drop : null;
      const movedId = dragOp.id;
      cancelDrag();
      if (!target) return;
      const moved = clusters.get(movedId);
      if (!moved) return;
      const move = resolveClusterMove(
        moved,
        anchor,
        target.position,
        entries,
        clusters
      );
      if (move) onClusterReorder(move);
    },
    [cancelDrag, clusters, dragOp, drop, entries, onClusterReorder]
  );

  return {
    state: {
      dragOp,
      drop,
      isCardMode: dragOp?.kind === "card",
      isClusterMode: dragOp?.kind === "cluster"
    },
    handlers: {
      startCard,
      startCluster,
      cardOver,
      cardDrop,
      clusterOver,
      clusterDrop,
      cancelDrag
    }
  };
}
