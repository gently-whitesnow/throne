/**
 * Tree layout: place each node in a column by longest path from a source,
 * then smooth Y inside each column toward the parents'/children's centroid.
 * Ported from the Prototype B v2 reference and made deterministic + cycle-safe.
 */

export const CARD_W = 248;
export const CARD_H = 86;
export const COL_GAP = 96;
export const ROW_GAP = 22;

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

  // 1. Longest-path levels from sources. Self-loops and missing parents are
  //    ignored; cycles are cut by remembering the active recursion stack.
  const level = new Map<string, number>();
  const compute = (id: string, stack: Set<string>): number => {
    const cached = level.get(id);
    if (cached !== undefined) return cached;
    if (stack.has(id)) return 0;
    stack.add(id);
    const node = byId.get(id);
    const parents = (node?.parents ?? []).filter(
      (p) => byId.has(p) && p !== id
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
  for (const n of nodes) compute(n.id, new Set());

  // 2. Group by column, preserving original ordering as a stable tie-breaker.
  const cols = new Map<number, string[]>();
  const ord = new Map<string, number>();
  for (let i = 0; i < nodes.length; i++) {
    const n = nodes[i];
    ord.set(n.id, i);
    const col = level.get(n.id) ?? 0;
    let bucket = cols.get(col);
    if (!bucket) {
      bucket = [];
      cols.set(col, bucket);
    }
    bucket.push(n.id);
  }
  const colKeys = [...cols.keys()].sort((a, b) => a - b);

  // 3. Initial Y stack per column.
  const pos = new Map<string, LayoutPosition>();
  for (const c of colKeys) {
    const ids = cols.get(c) ?? [];
    ids.sort((a, b) => (ord.get(a) ?? 0) - (ord.get(b) ?? 0));
    let y = 0;
    for (const id of ids) {
      pos.set(id, { x: c * (CARD_W + COL_GAP), y });
      y += CARD_H + ROW_GAP;
    }
  }

  const childrenOf = new Map<string, string[]>();
  for (const n of nodes) childrenOf.set(n.id, []);
  for (const n of nodes) {
    for (const p of n.parents) {
      if (p === n.id) continue;
      const bucket = childrenOf.get(p);
      if (bucket) bucket.push(n.id);
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

  let maxX = 0;
  let maxY = 0;
  for (const p of pos.values()) {
    if (p.x > maxX) maxX = p.x;
    if (p.y > maxY) maxY = p.y;
  }
  return {
    pos,
    bounds: {
      w: maxX + CARD_W,
      h: maxY + CARD_H,
      cols: colKeys.length
    }
  };
}
