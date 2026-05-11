import { useLayoutEffect, useState, type RefObject } from "react";

import type { LinksSummaryMap } from "../model/useLinksSummary";

interface OverlayProps {
  hoveredId: string | null;
  summary: LinksSummaryMap;
  rowRefs: ReadonlyMap<string, HTMLLIElement>;
  containerRef: RefObject<HTMLElement | null>;
  /** Width of the rail reserved on the right of the list (px). */
  railWidth: number;
  /** String that changes when row layout changes (visible-id signature). */
  layoutSignature: string;
}

interface ConnectorEdge {
  key: string;
  fromId: string;
  toId: string;
  d: string;
  stroke: string;
  dash?: string;
  baseOpacity: number;
  arrow?: "blocks" | "derived";
}

const LANE_SPACING = 6;

const STYLES = {
  blocks: {
    stroke: "var(--color-warning, #d97706)",
    baseOpacity: 0.85
  },
  derived: {
    stroke: "var(--color-base-content, #4c5567)",
    baseOpacity: 0.55
  },
  relates: {
    stroke: "var(--color-base-content, #4c5567)",
    dash: "3 3",
    baseOpacity: 0.35
  }
} as const;

/**
 * Always-on SVG rail rendered to the right of the list. Draws one rectangular
 * connector per (intent → peer) pair so dependencies are visible without
 * hovering. When a row is hovered, edges incident to that row keep their full
 * opacity while the rest dim — without that, dense graphs become noise.
 *
 * Geometry is a three-segment path: out from the card right edge into the
 * rail, vertical run inside the rail, back into the peer's right edge. Rail
 * lane offsets fan out per edge type so overlapping edges don't sit on top
 * of each other.
 */
export function IntentLinksOverlay({
  hoveredId,
  summary,
  rowRefs,
  containerRef,
  railWidth,
  layoutSignature
}: OverlayProps) {
  const edges = useEdges(
    summary,
    rowRefs,
    containerRef,
    railWidth,
    layoutSignature
  );

  if (edges.length === 0) return null;

  return (
    <svg
      aria-hidden
      className="pointer-events-none absolute inset-0 h-full w-full"
      preserveAspectRatio="none"
    >
      <defs>
        <marker
          id="arrow-blocks"
          viewBox="0 0 10 10"
          refX="8"
          refY="5"
          markerWidth="5"
          markerHeight="5"
          markerUnits="userSpaceOnUse"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 10 5 L 0 10 z" fill={STYLES.blocks.stroke} />
        </marker>
        <marker
          id="arrow-derived"
          viewBox="0 0 10 10"
          refX="8"
          refY="5"
          markerWidth="5"
          markerHeight="5"
          markerUnits="userSpaceOnUse"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 10 5 L 0 10 z" fill={STYLES.derived.stroke} />
        </marker>
      </defs>
      {edges.map((edge) => {
        const incident =
          hoveredId !== null &&
          (edge.fromId === hoveredId || edge.toId === hoveredId);
        const dimmed = hoveredId !== null && !incident;
        const opacity = dimmed ? 0.12 : incident ? 1 : edge.baseOpacity;
        const markerEnd = edge.arrow ? `url(#arrow-${edge.arrow})` : undefined;
        return (
          <path
            key={edge.key}
            d={edge.d}
            fill="none"
            stroke={edge.stroke}
            strokeWidth={incident ? 2 : 1.25}
            strokeDasharray={edge.dash}
            strokeLinecap="round"
            opacity={opacity}
            markerEnd={markerEnd}
          />
        );
      })}
    </svg>
  );
}

function useEdges(
  summary: LinksSummaryMap,
  rowRefs: ReadonlyMap<string, HTMLLIElement>,
  containerRef: RefObject<HTMLElement | null>,
  railWidth: number,
  layoutSignature: string
): ConnectorEdge[] {
  const [edges, setEdges] = useState<ConnectorEdge[]>([]);

  useLayoutEffect(() => {
    const container = containerRef.current;
    if (!container) {
      setEdges([]);
      return;
    }
    const recompute = () => {
      setEdges(buildEdges(summary, rowRefs, container, railWidth));
    };
    recompute();
    // Right-pane drag, sidebar collapse and font-zoom all change card widths
    // without touching summary/layoutSignature. ResizeObserver gives us a
    // re-measure trigger that is independent of React state churn.
    const observer = new ResizeObserver(recompute);
    observer.observe(container);
    return () => {
      observer.disconnect();
    };
  }, [summary, rowRefs, containerRef, railWidth, layoutSignature]);

  return edges;
}

interface EdgeSeed {
  fromId: string;
  toId: string;
  kindKey: string;
  lane: number;
  stroke: string;
  dash?: string;
  baseOpacity: number;
  arrow?: "blocks" | "derived";
}

function buildEdges(
  summary: LinksSummaryMap,
  rowRefs: ReadonlyMap<string, HTMLLIElement>,
  container: HTMLElement,
  railWidth: number
): ConnectorEdge[] {
  const seeds = collectSeeds(summary);
  if (seeds.length === 0) return [];

  const containerBox = container.getBoundingClientRect();
  const result: ConnectorEdge[] = [];
  for (const seed of seeds) {
    const fromEl = rowRefs.get(seed.fromId);
    const toEl = rowRefs.get(seed.toId);
    if (!fromEl || !toEl) continue;
    if (seed.fromId === seed.toId) continue;

    const fromBox = fromEl.getBoundingClientRect();
    const toBox = toEl.getBoundingClientRect();
    const cardRight = fromBox.right - containerBox.left;
    const peerRight = toBox.right - containerBox.left;
    const fromY = fromBox.top - containerBox.top + fromBox.height / 2;
    const toY = toBox.top - containerBox.top + toBox.height / 2;
    // Lane offset keeps simultaneous edges of different types from stacking
    // on top of each other. 6px per lane gives a readable gap for the eye.
    const laneX = cardRight + railWidth - 8 - seed.lane * LANE_SPACING;
    const enterX = cardRight - 2;
    // Reserve room for the arrow head on the receiving end.
    const exitX = peerRight - (seed.arrow ? 8 : 2);
    const r = 4;
    const dirIn = laneX > enterX ? 1 : -1;
    const dirOut = exitX < laneX ? -1 : 1;
    const vertical = Math.sign(toY - fromY || 1);
    const d =
      `M ${String(enterX)} ${String(fromY)} ` +
      `L ${String(laneX - r * dirIn)} ${String(fromY)} ` +
      `Q ${String(laneX)} ${String(fromY)} ${String(laneX)} ${String(fromY + r * vertical)} ` +
      `L ${String(laneX)} ${String(toY - r * vertical)} ` +
      `Q ${String(laneX)} ${String(toY)} ${String(laneX + r * dirOut)} ${String(toY)} ` +
      `L ${String(exitX)} ${String(toY)}`;

    result.push({
      key: `${seed.fromId}->${seed.toId}:${seed.kindKey}`,
      fromId: seed.fromId,
      toId: seed.toId,
      d,
      stroke: seed.stroke,
      dash: seed.dash,
      baseOpacity: seed.baseOpacity,
      arrow: seed.arrow
    });
  }
  return result;
}

function collectSeeds(summary: LinksSummaryMap): EdgeSeed[] {
  const seeds: EdgeSeed[] = [];
  // Directional edges (blocks / derived) appear on exactly one side of the
  // summary map, so they don't need pair de-dup. `relates` is symmetric and
  // surfaces from both endpoints — de-dup by canonical ordered pair.
  const seenRelates = new Set<string>();

  for (const [ownerId, entry] of summary) {
    // derived_from: ownerId is the child, peer is the parent — arrow flows from parent to child.
    for (const peer of entry.derived_from) {
      seeds.push({
        fromId: peer.id,
        toId: ownerId,
        kindKey: "derived",
        lane: 0,
        stroke: STYLES.derived.stroke,
        baseOpacity: STYLES.derived.baseOpacity,
        arrow: "derived"
      });
    }
    // blocked_by: peer blocks ownerId — arrow points at ownerId (the blocked one).
    for (const peer of entry.blocked_by) {
      seeds.push({
        fromId: peer.id,
        toId: ownerId,
        kindKey: "blocks",
        lane: 1,
        stroke: STYLES.blocks.stroke,
        baseOpacity: STYLES.blocks.baseOpacity,
        arrow: "blocks"
      });
    }
    // relates: symmetric, no arrow.
    for (const peer of entry.relates) {
      const a = ownerId < peer.id ? ownerId : peer.id;
      const b = ownerId < peer.id ? peer.id : ownerId;
      const key = `${a}|${b}`;
      if (seenRelates.has(key)) continue;
      seenRelates.add(key);
      seeds.push({
        fromId: ownerId,
        toId: peer.id,
        kindKey: "relates",
        lane: 2,
        stroke: STYLES.relates.stroke,
        dash: STYLES.relates.dash,
        baseOpacity: STYLES.relates.baseOpacity
      });
    }
  }
  return seeds;
}
