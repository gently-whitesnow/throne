/**
 * Decorative accent for a context-part block, keyed by its scope (system / user).
 * Both surfaces that list parts — the `/agent-context` board and the preflight
 * modal — render the same scope in the same colour, so the system/user boundary
 * reads at a glance without parsing the scope badge.
 *
 * Reuses the decorative `--color-family-*` tokens (deliberately off the semantic
 * status axes, so the accent never mimics done/reject/active). Rendered as the
 * intent-board family pattern: a saturated left-edge stripe plus a very light
 * background tint. Colour is a redundant channel here — the scope label stays,
 * so it never carries meaning alone.
 */
const SCOPE_FAMILY: Record<string, string> = {
  system: "var(--color-family-6)", // sky
  user: "var(--color-family-2)" // pink
};

export interface ScopeAccent {
  /** Saturated left-edge stripe colour. */
  stripe: string;
  /** Very light block-background tint mixed over base-100. */
  tint: string;
}

export function scopeAccent(scope: string): ScopeAccent | null {
  const family = SCOPE_FAMILY[scope];
  if (!family) return null;
  return {
    stripe: family,
    tint: `color-mix(in oklch, ${family} 6%, var(--color-base-100))`
  };
}
