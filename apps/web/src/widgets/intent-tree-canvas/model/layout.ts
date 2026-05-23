/**
 * Tree layout: place each node in a column by longest path from a source,
 * then smooth Y inside each column toward the parents'/children's centroid.
 * Ported from the Prototype B v2 reference and made deterministic + cycle-safe.
 */

export const CARD_W = 248;
export const CARD_H = 104;
export const COL_GAP = 96;
export const ROW_GAP = 22;
// Gap between the connected DAG and the strip of isolated cards parked below
// it. Big enough to read as a separate group, small enough not to look like a
// scroll-jump.
const ISOLATED_GAP = ROW_GAP * 2;

export interface LayoutNode {
  id: string;
  parents: readonly string[];
}

export interface LayoutPosition {
  x: number;
  y: number;
}

export interface LayoutBounds {
  w: number;
  h: number;
  cols: number;
}

export interface LayoutResult {
  pos: ReadonlyMap<string, LayoutPosition>;
  bounds: LayoutBounds;
}

export function layoutTree(nodes: readonly LayoutNode[]): LayoutResult {
  if (nodes.length === 0) {
    return { pos: new Map(), bounds: { w: 0, h: 0, cols: 0 } };
  }

  const byId = new Map<string, LayoutNode>();
  for (const n of nodes) byId.set(n.id, n);

  // Children sets — needed to detect isolated nodes (no real parents, no real
  // children in the visible set). Self-loops and dangling refs are filtered.
  const childrenOf = new Map<string, string[]>();
  for (const n of nodes) childrenOf.set(n.id, []);
  for (const n of nodes) {
    for (const p of n.parents) {
      if (p === n.id) continue;
      if (!byId.has(p)) continue;
      childrenOf.get(p)?.push(n.id);
    }
  }

  // Partition: isolated cards skip the main DAG layout entirely and get parked
  // in a horizontal strip below it. Otherwise the smoothing pass drags the
  // connected spine down to centre on its descendants and the isolated stack
  // (which can't be smoothed because it has no edges) ends up far above the
  // spine — exactly the «остальные интенты далеко от нижнего» complaint.
  const connectedIds: string[] = [];
  const isolatedIds: string[] = [];
  const isolatedSet = new Set<string>();
  for (const n of nodes) {
    const realParents = n.parents.filter((p) => byId.has(p) && p !== n.id);
    const realChildren = childrenOf.get(n.id) ?? [];
    if (realParents.length === 0 && realChildren.length === 0) {
      isolatedIds.push(n.id);
      isolatedSet.add(n.id);
    } else {
      connectedIds.push(n.id);
    }
  }

  // 1. Longest-path levels from sources, restricted to connected nodes.
  //    Self-loops and missing parents are ignored; cycles are cut by
  //    remembering the active recursion stack.
  const level = new Map<string, number>();
  const compute = (id: string, stack: Set<string>): number => {
    const cached = level.get(id);
    if (cached !== undefined) return cached;
    if (stack.has(id)) return 0;
    stack.add(id);
    const node = byId.get(id);
    const parents = (node?.parents ?? []).filter(
      (p) => byId.has(p) && p !== id && !isolatedSet.has(p)
    );
    let value = 0;
    if (parents.length > 0) {
      let best = -1;
      for (const p of parents) {
        const lvl = compute(p, stack);
        if (lvl > best) best = lvl;
      }
      value = best + 1;
    }
    level.set(id, value);
    stack.delete(id);
    return value;
  };
  for (const id of connectedIds) compute(id, new Set());

  // 2. Group connected nodes by column, preserving original ordering as a
  //    stable tie-breaker for descCount ties later.
  const cols = new Map<number, string[]>();
  const ord = new Map<string, number>();
  for (let i = 0; i < connectedIds.length; i++) {
    const id = connectedIds[i];
    ord.set(id, i);
    const col = level.get(id) ?? 0;
    let bucket = cols.get(col);
    if (!bucket) {
      bucket = [];
      cols.set(col, bucket);
    }
    bucket.push(id);
  }
  const colKeys = [...cols.keys()].sort((a, b) => a - b);

  // Transitive descendant count — used to seat well-connected nodes at the top
  // of their column so chain heads anchor the layout and orphans drift below
  // instead of pushing the chain off the bottom.
  const descCount = new Map<string, number>();
  const countDesc = (id: string, stack: Set<string>): number => {
    const cached = descCount.get(id);
    if (cached !== undefined) return cached;
    if (stack.has(id)) return 0;
    stack.add(id);
    const kids = childrenOf.get(id) ?? [];
    const visited = new Set<string>();
    let total = 0;
    for (const k of kids) {
      if (k === id || visited.has(k)) continue;
      visited.add(k);
      total += 1 + countDesc(k, stack);
    }
    stack.delete(id);
    descCount.set(id, total);
    return total;
  };
  for (const id of connectedIds) countDesc(id, new Set());

  // 3. Initial Y stack per column. Within a column, place nodes with more
  //    descendants first so the spine of the DAG forms a clean top row, and
  //    orphans / leaves fall in below.
  const pos = new Map<string, LayoutPosition>();
  for (const c of colKeys) {
    const ids = cols.get(c) ?? [];
    ids.sort((a, b) => {
      const da = descCount.get(a) ?? 0;
      const db = descCount.get(b) ?? 0;
      if (da !== db) return db - da;
      return (ord.get(a) ?? 0) - (ord.get(b) ?? 0);
    });
    let y = 0;
    for (const id of ids) {
      pos.set(id, { x: c * (CARD_W + COL_GAP), y });
      y += CARD_H + ROW_GAP;
    }
  }

  const smoothDown = (): void => {
    for (const c of colKeys) {
      if (c === 0) continue;
      const ids = cols.get(c) ?? [];
      const desired = new Map<string, number>();
      for (const id of ids) {
        const ps = (byId.get(id)?.parents ?? []).filter((p) => pos.has(p));
        if (ps.length > 0) {
          let sum = 0;
          for (const p of ps) sum += pos.get(p)?.y ?? 0;
          desired.set(id, sum / ps.length);
        } else {
          desired.set(id, pos.get(id)?.y ?? 0);
        }
      }
      const ordered = [...ids].sort(
        (a, b) => (desired.get(a) ?? 0) - (desired.get(b) ?? 0)
      );
      let cursor = -Infinity;
      for (const id of ordered) {
        let y = desired.get(id) ?? 0;
        if (y < cursor) y = cursor;
        const prev = pos.get(id);
        pos.set(id, { x: prev?.x ?? 0, y });
        cursor = y + CARD_H + ROW_GAP;
      }
    }
  };

  const smoothUp = (): void => {
    for (let i = colKeys.length - 1; i >= 0; i--) {
      const c = colKeys[i];
      const ids = cols.get(c) ?? [];
      const desired = new Map<string, number>();
      for (const id of ids) {
        const ch = (childrenOf.get(id) ?? []).filter((k) => pos.has(k));
        if (ch.length > 0) {
          let sum = 0;
          for (const k of ch) sum += pos.get(k)?.y ?? 0;
          desired.set(id, sum / ch.length);
        } else {
          desired.set(id, pos.get(id)?.y ?? 0);
        }
      }
      const ordered = [...ids].sort(
        (a, b) => (desired.get(a) ?? 0) - (desired.get(b) ?? 0)
      );
      let cursor = -Infinity;
      for (const id of ordered) {
        let y = desired.get(id) ?? 0;
        if (y < cursor) y = cursor;
        const prev = pos.get(id);
        pos.set(id, { x: prev?.x ?? 0, y });
        cursor = y + CARD_H + ROW_GAP;
      }
    }
  };

  for (let i = 0; i < 6; i++) {
    smoothDown();
    smoothUp();
  }
  smoothDown();

  // 4. Normalize Y so the topmost card sits at y = 0.
  let minY = Infinity;
  for (const p of pos.values()) if (p.y < minY) minY = p.y;
  if (Number.isFinite(minY) && minY !== 0) {
    for (const [id, p] of pos) pos.set(id, { x: p.x, y: p.y - minY });
  }

  let connectedMaxX = 0;
  let connectedMaxY = 0;
  for (const p of pos.values()) {
    if (p.x > connectedMaxX) connectedMaxX = p.x;
    if (p.y > connectedMaxY) connectedMaxY = p.y;
  }
  const connectedW = pos.size > 0 ? connectedMaxX + CARD_W : 0;
  const connectedH = pos.size > 0 ? connectedMaxY + CARD_H : 0;

  // 5. Isolated strip. Pack the cards into rows that fit within the DAG's
  //    width (or the width of one card column when the DAG is empty), placed
  //    directly under the spine with a small visual gap.
  let isolatedUsedCols = 0;
  if (isolatedIds.length > 0) {
    const rowStartY = connectedH > 0 ? connectedH + ISOLATED_GAP : 0;
    const targetRowWidth = Math.max(connectedW, CARD_W);
    const stride = CARD_W + COL_GAP;
    const perRow = Math.max(1, Math.floor((targetRowWidth + COL_GAP) / stride));
    isolatedIds.forEach((id, i) => {
      const row = Math.floor(i / perRow);
      const col = i % perRow;
      pos.set(id, {
        x: col * stride,
        y: rowStartY + row * (CARD_H + ROW_GAP)
      });
    });
    isolatedUsedCols = Math.min(perRow, isolatedIds.length);
  }

  let maxX = 0;
  let maxY = 0;
  for (const p of pos.values()) {
    if (p.x > maxX) maxX = p.x;
    if (p.y > maxY) maxY = p.y;
  }
  return {
    pos,
    bounds: {
      w: pos.size > 0 ? maxX + CARD_W : 0,
      h: pos.size > 0 ? maxY + CARD_H : 0,
      cols: Math.max(colKeys.length, isolatedUsedCols)
    }
  };
}
