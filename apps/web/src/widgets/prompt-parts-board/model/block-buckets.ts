import type {
  PromptPartListItem,
  PromptPartMode,
  PromptPartUiRole
} from "@/entities/prompt-part";

/**
 * Where a block lands for a given mode: included (ships in the prompt),
 * available (off by default, operator can switch it on), excluded (not bound).
 */
export type BlockBucket = "included" | "available" | "excluded";

export function roleForMode(
  part: PromptPartListItem,
  mode: PromptPartMode
): PromptPartUiRole {
  return (
    (part.mode_roles.find((r) => r.mode === mode)?.role as
      | PromptPartUiRole
      | undefined) ?? "none"
  );
}

export function bucketForRole(role: PromptPartUiRole): BlockBucket {
  if (role === "mandatory" || role === "default_on") return "included";
  if (role === "default_off") return "available";
  return "excluded";
}

export interface ModeBuckets {
  included: PromptPartListItem[];
  available: PromptPartListItem[];
  excluded: PromptPartListItem[];
}

export function bucketize(
  parts: PromptPartListItem[],
  mode: PromptPartMode
): ModeBuckets {
  const buckets: ModeBuckets = { included: [], available: [], excluded: [] };
  for (const part of parts) {
    buckets[bucketForRole(roleForMode(part, mode))].push(part);
  }
  return buckets;
}

/** How many blocks actually ship in the prompt for this mode (rail counter). */
export function includedCount(
  parts: PromptPartListItem[],
  mode: PromptPartMode
): number {
  return parts.reduce(
    (n, part) =>
      bucketForRole(roleForMode(part, mode)) === "included" ? n + 1 : n,
    0
  );
}
