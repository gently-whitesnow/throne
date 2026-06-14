import type { LinksSummaryMap } from "@/entities/intent";

/**
 * Family-marker colours for intents that share the same `derived_from`
 * ancestor. The host (EntityList) renders this as a thin left-edge stripe,
 * not as a row background — full-row tints clashed with the active-state
 * highlight (`bg-primary/10`) and with the status palette. The stripe lives
 * in a visual channel that the row otherwise doesn't use.
 *
 * The "family root" is the topmost ancestor reachable via `derived_from`
 * within the visible set. If a card's parent is off-screen, that off-screen
 * id becomes the root — siblings still resolve to the same id and group up.
 *
 * Hues deliberately avoid blue (= active/primary), pure green (= done/work),
 * and red/orange (= reject/warning) so the stripe never mimics a status.
 */
const PALETTE: readonly string[] = [
  "var(--color-family-1)", // purple
  "var(--color-family-2)", // pink
  "var(--color-family-3)", // teal
  "var(--color-family-4)", // violet
  "var(--color-family-5)", // fuchsia
  "var(--color-family-6)" // sky
];

export function computeFamilyTints(
  visibleIds: readonly string[],
  summary: LinksSummaryMap
): ReadonlyMap<string, string> {
  if (visibleIds.length === 0) return new Map();

  const visible = new Set(visibleIds);
  const rootCache = new Map<string, string>();

  function rootOf(id: string): string {
    const cached = rootCache.get(id);
    if (cached !== undefined) return cached;
    const seen: string[] = [];
    let current = id;
    let cycle = false;
    for (;;) {
      if (seen.includes(current)) {
        cycle = true;
        break;
      }
      seen.push(current);
      const entry = visible.has(current) ? summary.get(current) : undefined;
      const parent = entry?.derived_from[0]?.id;
      if (!parent) break;
      current = parent;
    }
    // On cycles `current` is ambiguous — collapse to the lexicographically
    // smallest id in the walk so every member of the cycle resolves to the
    // same root and ends up in the same family.
    const root = cycle ? ([...seen].sort()[0] ?? current) : current;
    rootCache.set(id, root);
    return root;
  }

  const groups = new Map<string, string[]>();
  for (const id of visibleIds) {
    const root = rootOf(id);
    const list = groups.get(root);
    if (list) list.push(id);
    else groups.set(root, [id]);
  }

  // Stable colour assignment: order families by their root id so the same
  // visible set always gets the same colours across renders.
  const sortedRoots = [...groups.keys()].sort();
  const tints = new Map<string, string>();
  let colourIdx = 0;
  for (const root of sortedRoots) {
    const members = groups.get(root) ?? [];
    if (members.length < 2) continue;
    const colour = PALETTE[colourIdx % PALETTE.length];
    colourIdx += 1;
    for (const id of members) tints.set(id, colour);
  }
  return tints;
}
