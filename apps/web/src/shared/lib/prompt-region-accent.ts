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
 * saturated left-edge stripe plus a very light zone-background tint. Colour is a
 * redundant channel — the zone headers stay labelled, so it never carries
 * meaning alone.
 */
export type PromptRegion = "system" | "user";

// Tint is mixed in sRGB, not oklch: base-100 is a faintly pink white, and oklch
// hue interpolation keeps a low-percentage mix near the base hue — a 9% sky mix
// still came out pink, not blue. sRGB blends toward the channel that's actually
// blue/pink, so the tint reads as the stripe colour.
//
// Per-region strength differs because pink (family-2) is more saturated than sky
// (family-6): sky is pushed up so the system zone shows a faint blue matching its
// stripe; pink is pulled down so the user zone stays muted.
const REGION: Record<PromptRegion, { family: string; tintPct: string }> = {
  system: { family: "var(--color-family-6)", tintPct: "10%" }, // sky
  user: { family: "var(--color-family-2)", tintPct: "4%" } // pink
};

export interface PromptRegionAccent {
  /** Left-edge stripe / header-label colour, eased off full saturation. */
  stripe: string;
  /** Very light zone-background tint mixed over base-100. */
  tint: string;
}

export function promptRegionAccent(region: PromptRegion): PromptRegionAccent {
  const { family, tintPct } = REGION[region];
  return {
    stripe: `color-mix(in srgb, ${family} 85%, var(--color-base-100))`,
    tint: `color-mix(in srgb, ${family} ${tintPct}, var(--color-base-100))`
  };
}
