import { useMemo } from "react";

import { usePromptPartPatchesList } from "@/entities/prompt-part-patch";

export interface PartPatchCounts {
  /** key `${scope}/${key}` → number of proposed patches targeting that part. */
  counts: Map<string, number>;
  /** Total proposed patches across all parts (drives the slot badge). */
  total: number;
}

export function partPatchKey(scope: string, key: string): string {
  return `${scope}/${key}`;
}

/**
 * Counts proposed prompt-part patches per target part so the System slot can
 * surface «N правок» next to each part. Shares the entity query cache, so
 * calling it in several places does not double-fetch.
 */
export function usePartPatchCounts(): PartPatchCounts {
  const query = usePromptPartPatchesList({ status: "proposed", limit: 200 });

  return useMemo<PartPatchCounts>(() => {
    const items = query.data?.items ?? [];
    const counts = new Map<string, number>();
    for (const patch of items) {
      const key = partPatchKey(patch.target_scope, patch.target_key);
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    return { counts, total: items.length };
  }, [query.data]);
}
