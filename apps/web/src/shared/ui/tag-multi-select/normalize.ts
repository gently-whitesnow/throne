const SLUG_RE = /^[a-z0-9][a-z0-9_-]*$/;
const WS_RE = /\s+/g;

export const TAG_NAME_MAX_LENGTH = 40;

export interface TagSlugResult {
  slug: string;
  valid: boolean;
  reason: "empty" | "too-long" | "bad-chars" | null;
}

export function normalizeTagSlug(raw: string): TagSlugResult {
  const trimmed = raw.trim().replace(/^#+/, "").trim();
  if (trimmed.length === 0) {
    return { slug: "", valid: false, reason: "empty" };
  }
  const collapsed = trimmed.toLowerCase().replace(WS_RE, "-");
  if (collapsed.length > TAG_NAME_MAX_LENGTH) {
    return { slug: collapsed, valid: false, reason: "too-long" };
  }
  if (!SLUG_RE.test(collapsed)) {
    return { slug: collapsed, valid: false, reason: "bad-chars" };
  }
  return { slug: collapsed, valid: true, reason: null };
}
