/**
 * Decorative accent for a prompt region — the two zones the operator assembles
 * for a run: the system-prompt zone and the user-prompt zone. Both surfaces that
 * lay these zones out side by side — the `/agent-context` board and the preflight
 * modal — colour the same region the same way, so «где кончается system и
 * начинается user» reads at a glance without parsing block labels.
 *
 * Colour keys on the *zone*, not on an individual block's scope: a user-authored
 * block that ships inside the system prompt still belongs to the system zone, so
 * the whole zone stays one colour instead of a per-block patchwork.
 *
 * Reuses the decorative `--color-family-*` tokens (deliberately off the semantic
 * status axes, so the accent never mimics done/reject/active). Rendered as a
 * single left-edge stripe (no background fill). Colour is a redundant channel —
 * the zone headers stay labelled, so it never carries meaning alone.
 */
export type PromptRegion = "system" | "user";

const REGION_FAMILY: Record<PromptRegion, string> = {
  system: "var(--color-family-6)", // sky
  user: "var(--color-family-2)" // pink
};

export interface PromptRegionAccent {
  /** Left-edge stripe colour, eased off full saturation. */
  stripe: string;
}

export function promptRegionAccent(region: PromptRegion): PromptRegionAccent {
  return {
    stripe: `color-mix(in srgb, ${REGION_FAMILY[region]} 85%, var(--color-base-100))`
  };
}
