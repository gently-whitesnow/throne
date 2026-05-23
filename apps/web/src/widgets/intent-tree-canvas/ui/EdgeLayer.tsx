import { useMemo } from "react";

import { CARD_H, CARD_W, type LayoutPosition } from "../model/layout";

interface EdgeLayerProps {
  edges: readonly Edge[];
  positions: ReadonlyMap<string, LayoutPosition>;
  /** Ids of nodes considered "related" to the active selection (ancestors + descendants + self). */
  relatedIds: ReadonlySet<string> | null;
  width: number;
  height: number;
}

export interface Edge {
  from: string;
  to: string;
}

export function EdgeLayer({
  edges,
  positions,
  relatedIds,
  width,
  height
}: EdgeLayerProps) {
  const paths = useMemo(() => {
    const out: { d: string; related: boolean; key: string }[] = [];
    for (const e of edges) {
      const a = positions.get(e.from);
      const b = positions.get(e.to);
      if (!a || !b) continue;
      const x1 = a.x + CARD_W;
      const y1 = a.y + CARD_H / 2;
      const x2 = b.x;
      const y2 = b.y + CARD_H / 2;
      const mx = (x1 + x2) / 2;
      const d = `M ${String(x1)} ${String(y1)} C ${String(mx)} ${String(y1)}, ${String(mx)} ${String(y2)}, ${String(x2)} ${String(y2)}`;
      const related =
        relatedIds !== null && (relatedIds.has(e.from) || relatedIds.has(e.to));
      out.push({ d, related, key: `${e.from}->${e.to}` });
    }
    return out;
  }, [edges, positions, relatedIds]);

  return (
    <svg
      className="pointer-events-none absolute left-0 top-0"
      width={width}
      height={height}
      style={{ overflow: "visible" }}
      aria-hidden
    >
      {paths.map((p) => {
        // Without a selection, every edge gets the neutral look at full
        // opacity. With one, related edges go primary + bright, the rest stay
        // dimmed but visible so the graph never looks washed out.
        const hasSelection = relatedIds !== null;
        const isRelated = hasSelection && p.related;
        return (
          <path
            key={p.key}
            d={p.d}
            fill="none"
            stroke={
              isRelated
                ? "var(--color-primary, oklch(0.5 0.2 255))"
                : "var(--color-base-content, oklch(0.2 0.02 250))"
            }
            strokeWidth={isRelated ? 2 : 1.4}
            opacity={!hasSelection || isRelated ? 0.6 : 0.25}
            vectorEffect="non-scaling-stroke"
          />
        );
      })}
    </svg>
  );
}
