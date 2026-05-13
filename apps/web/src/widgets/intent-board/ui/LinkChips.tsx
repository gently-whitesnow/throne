import { ArrowDown, ArrowUp } from "lucide-react";

import type { LinksSummaryMap } from "../model/useLinksSummary";

interface LinkChipsProps {
  summary: LinksSummaryMap;
  intentId: string;
  /** Maps intent id → cluster id; used to decide whether to surface a chip. */
  clusterByIntent: ReadonlyMap<string, string>;
}

/**
 * Inline chips that surface DAG context for a card inside a cluster: «↑ from:
 * N» when the node has more than one derived_from parent, and «↓ blocks: N»
 * when it blocks more than one peer. Chips replace the in-cluster arrows
 * that used to draw between member rows — the same information without the
 * visual noise.
 *
 * Chips intentionally don't appear for cards outside clusters: those still
 * use the right-side rail (`IntentLinksOverlay`) so first-time readers see
 * directionality, not abbreviations.
 */
export function LinkChips({
  summary,
  intentId,
  clusterByIntent
}: LinkChipsProps) {
  const myCluster = clusterByIntent.get(intentId);
  if (!myCluster) return null;
  const entry = summary.get(intentId);
  if (!entry) return null;

  const intraParents = entry.derived_from.filter(
    (p) => clusterByIntent.get(p.id) === myCluster
  );
  const intraBlocks = entry.source_of.filter((p) => {
    // source_of points at descendants we «source» via derived_from. Not a
    // «blocks» edge — for blocks-direction we use peers that own a `blocked_by`
    // entry pointing at us, which the summary surfaces from their side. For
    // the chip we approximate with derived children (this matches the
    // post-collapse «↓ blocks» intent in the spec: «N nodes downstream»).
    return clusterByIntent.get(p.id) === myCluster;
  });

  const parentCount = intraParents.length;
  const blockCount = intraBlocks.length;

  if (parentCount <= 1 && blockCount <= 1) return null;

  return (
    <span className="flex flex-shrink-0 items-center gap-1">
      {parentCount > 1 ? (
        <span
          className="inline-flex items-center gap-0.5 rounded border border-base-300 bg-base-200 px-1 py-px text-[10px] font-medium text-base-content/70"
          title={`Родительских узлов в кластере: ${String(parentCount)}`}
        >
          <ArrowUp aria-hidden size={10} strokeWidth={2} />
          {parentCount}
        </span>
      ) : null}
      {blockCount > 1 ? (
        <span
          className="inline-flex items-center gap-0.5 rounded border border-warning/30 bg-warning/10 px-1 py-px text-[10px] font-medium text-warning"
          title={`Узлов ниже по графу в кластере: ${String(blockCount)}`}
        >
          <ArrowDown aria-hidden size={10} strokeWidth={2} />
          {blockCount}
        </span>
      ) : null}
    </span>
  );
}
